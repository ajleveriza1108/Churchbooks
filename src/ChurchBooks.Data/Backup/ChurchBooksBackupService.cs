using System.Security.Cryptography;
using System.Text;
using ChurchBooks.Data.Storage;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Backup;

public sealed record ChurchBooksBackupResult(
    string BackupPath,
    DateTimeOffset CreatedUtc,
    long SizeBytes,
    string Sha256,
    string ValidationMessage);

public sealed record ChurchBooksBackupValidationResult(
    bool IsValid,
    string Message,
    string OrganizationName,
    string SchemaPhase);

public sealed record ChurchBooksRestoreResult(
    string RestoredFromPath,
    string SafetyBackupPath,
    DateTimeOffset RestoredUtc,
    string ValidationMessage);

/// <summary>
/// Creates and restores SQLite-consistent ChurchBooks backups using SQLite's online backup API.
/// Raw database-file copying is intentionally avoided because the live database uses WAL mode.
/// </summary>
public sealed class ChurchBooksBackupService
{
    public const string BackupExtension = ".cbbackup";
    private readonly ChurchBooksDatabase _database;

    public ChurchBooksBackupService(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<ChurchBooksBackupResult> CreateBackupAsync(
        string destinationDirectory,
        string organizationToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("Choose a backup folder before creating a backup.", nameof(destinationDirectory));
        }
        if (!File.Exists(_database.DatabasePath))
        {
            throw new FileNotFoundException("The live ChurchBooks database does not exist yet.", _database.DatabasePath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destinationDirectory);

        var token = SanitizeFileToken(organizationToken);
        var stamp = DateTimeOffset.UtcNow;
        var backupPath = BuildUniqueBackupPath(destinationDirectory, token, stamp);

        try
        {
            await using var source = _database.CreateConnection();
            await source.OpenAsync(cancellationToken);

            await using var destination = CreateConnection(backupPath, SqliteOpenMode.ReadWriteCreate);
            await destination.OpenAsync(cancellationToken);

            // BackupDatabase captures the committed SQLite state safely even when the source uses WAL.
            source.BackupDatabase(destination);
            cancellationToken.ThrowIfCancellationRequested();

            var validation = await ValidateOpenConnectionAsync(destination, cancellationToken);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("The new backup failed SQLite validation: " + validation.Message);
            }
        }
        catch
        {
            TryDelete(backupPath);
            throw;
        }

        var hash = await ComputeSha256Async(backupPath, cancellationToken);
        var info = new FileInfo(backupPath);
        return new ChurchBooksBackupResult(
            backupPath,
            stamp,
            info.Length,
            hash,
            "SQLite quick_check passed and the ChurchBooks schema marker is present.");
    }

    public async Task<ChurchBooksBackupValidationResult> ValidateBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            return new ChurchBooksBackupValidationResult(false, "The selected backup file does not exist.", string.Empty, string.Empty);
        }

        try
        {
            await using var connection = CreateConnection(backupPath, SqliteOpenMode.ReadOnly);
            await connection.OpenAsync(cancellationToken);
            return await ValidateOpenConnectionAsync(connection, cancellationToken);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ChurchBooksBackupValidationResult(false, "The selected file is not a valid readable ChurchBooks backup: " + ex.Message, string.Empty, string.Empty);
        }
    }

    public async Task<ChurchBooksRestoreResult> RestoreBackupAsync(
        string backupPath,
        string safetyBackupDirectory,
        string organizationToken,
        CancellationToken cancellationToken = default)
    {
        if (Path.GetFullPath(backupPath).Equals(Path.GetFullPath(_database.DatabasePath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The live ChurchBooks database cannot be selected as its own restore source.");
        }

        var selectedValidation = await ValidateBackupAsync(backupPath, cancellationToken);
        if (!selectedValidation.IsValid)
        {
            throw new InvalidOperationException(selectedValidation.Message);
        }

        // A verified safety backup is mandatory before the live books can be replaced.
        var safety = await CreateBackupAsync(
            safetyBackupDirectory,
            "PreRestore-" + SanitizeFileToken(organizationToken),
            cancellationToken);

        try
        {
            await RestoreCoreAsync(backupPath, cancellationToken);
            var liveValidation = await ValidateBackupAsync(_database.DatabasePath, cancellationToken);
            if (!liveValidation.IsValid)
            {
                throw new InvalidOperationException("The restored database failed validation: " + liveValidation.Message);
            }

            return new ChurchBooksRestoreResult(
                backupPath,
                safety.BackupPath,
                DateTimeOffset.UtcNow,
                liveValidation.Message);
        }
        catch
        {
            // Fail closed: if restore fails after the safety backup was created, put the exact pre-restore books back.
            try
            {
                await RestoreCoreAsync(safety.BackupPath, CancellationToken.None);
            }
            catch
            {
                // Preserve the original restore exception; the verified safety backup path remains available to the operator.
            }
            throw;
        }
    }

    private async Task RestoreCoreAsync(string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var source = CreateConnection(sourcePath, SqliteOpenMode.ReadOnly);
        await source.OpenAsync(cancellationToken);
        await using var destination = _database.CreateConnection();
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        cancellationToken.ThrowIfCancellationRequested();

        await using var checkpoint = destination.CreateCommand();
        checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await checkpoint.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ChurchBooksBackupValidationResult> ValidateOpenConnectionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var quickCheck = connection.CreateCommand())
        {
            quickCheck.CommandText = "PRAGMA quick_check;";
            await using var reader = await quickCheck.ExecuteReaderAsync(cancellationToken);
            var rows = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
            }
            if (rows.Count != 1 || !string.Equals(rows[0], "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new ChurchBooksBackupValidationResult(false, "SQLite quick_check did not return 'ok'.", string.Empty, string.Empty);
            }
        }

        if (!await TableExistsAsync(connection, "app_schema", cancellationToken))
        {
            return new ChurchBooksBackupValidationResult(false, "The ChurchBooks app_schema marker is missing.", string.Empty, string.Empty);
        }

        var schemaPhase = string.Empty;
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key = 'phase' LIMIT 1;";
            var scalar = await schema.ExecuteScalarAsync(cancellationToken);
            schemaPhase = Convert.ToString(scalar, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        var organization = string.Empty;
        if (await TableExistsAsync(connection, "organization_profile", cancellationToken))
        {
            await using var org = connection.CreateCommand();
            org.CommandText = "SELECT display_name FROM organization_profile WHERE profile_key = 'PRIMARY' LIMIT 1;";
            organization = Convert.ToString(await org.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return new ChurchBooksBackupValidationResult(
            true,
            "SQLite quick_check passed and the ChurchBooks schema marker is present.",
            organization,
            schemaPhase);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static SqliteConnection CreateConnection(string path, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Cache = SqliteCacheMode.Default,
            Pooling = false
        };
        return new SqliteConnection(builder.ToString());
    }

    private static string BuildUniqueBackupPath(string directory, string token, DateTimeOffset stamp)
    {
        var baseName = $"ChurchBooks-{token}-{stamp:yyyyMMdd-HHmmssfff}";
        var path = Path.Combine(directory, baseName + BackupExtension);
        if (!File.Exists(path))
        {
            return path;
        }
        return Path.Combine(directory, baseName + "-" + Guid.NewGuid().ToString("N")[..8] + BackupExtension);
    }

    private static string SanitizeFileToken(string value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "Organization" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            builder.Append(invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character);
        }
        var token = builder.ToString().Trim('-');
        while (token.Contains("--", StringComparison.Ordinal))
        {
            token = token.Replace("--", "-", StringComparison.Ordinal);
        }
        return string.IsNullOrWhiteSpace(token) ? "Organization" : token;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var algorithm = SHA256.Create();
        var hash = await algorithm.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup only. The original operation exception remains authoritative.
        }
    }
}
