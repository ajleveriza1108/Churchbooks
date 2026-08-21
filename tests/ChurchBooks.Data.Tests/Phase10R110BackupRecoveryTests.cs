using ChurchBooks.Data.Backup;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class Phase10R110BackupRecoveryTests
{
    [Fact]
    public async Task CreateBackupAsync_ProducesIndependentVerifiedSqliteSnapshot()
    {
        using var scope = new TemporaryDirectory();
        var livePath = Path.Combine(scope.Path, "live.db");
        var database = new ChurchBooksDatabase(livePath);
        await database.InitializeAsync();
        await ExecuteAsync(database, "CREATE TABLE backup_probe(value TEXT NOT NULL); INSERT INTO backup_probe(value) VALUES('snapshot');");

        var service = new ChurchBooksBackupService(database);
        var result = await service.CreateBackupAsync(Path.Combine(scope.Path, "backups"), "BFBC");

        Assert.True(File.Exists(result.BackupPath));
        Assert.EndsWith(ChurchBooksBackupService.BackupExtension, result.BackupPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.SizeBytes > 0);
        Assert.Equal(64, result.Sha256.Length);
        var validation = await service.ValidateBackupAsync(result.BackupPath);
        Assert.True(validation.IsValid, validation.Message);

        await using var backup = CreateReadOnly(result.BackupPath);
        await backup.OpenAsync();
        await using var command = backup.CreateCommand();
        command.CommandText = "SELECT value FROM backup_probe LIMIT 1;";
        Assert.Equal("snapshot", Convert.ToString(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task ValidateBackupAsync_RejectsRandomTextFileWithoutChangingLiveDatabase()
    {
        using var scope = new TemporaryDirectory();
        var livePath = Path.Combine(scope.Path, "live.db");
        var database = new ChurchBooksDatabase(livePath);
        await database.InitializeAsync();
        var invalid = Path.Combine(scope.Path, "not-a-backup.cbbackup");
        await File.WriteAllTextAsync(invalid, "not sqlite");

        var service = new ChurchBooksBackupService(database);
        var validation = await service.ValidateBackupAsync(invalid);

        Assert.False(validation.IsValid);
        Assert.True(File.Exists(livePath));
    }

    [Fact]
    public async Task RestoreBackupAsync_CreatesSafetyBackupBeforeReplacingLiveBooks()
    {
        using var scope = new TemporaryDirectory();
        var livePath = Path.Combine(scope.Path, "live.db");
        var backupDirectory = Path.Combine(scope.Path, "backups");
        var database = new ChurchBooksDatabase(livePath);
        await database.InitializeAsync();
        await ExecuteAsync(database, "CREATE TABLE restore_probe(value TEXT NOT NULL); INSERT INTO restore_probe(value) VALUES('before');");
        var service = new ChurchBooksBackupService(database);
        var snapshot = await service.CreateBackupAsync(backupDirectory, "BFBC");
        await ExecuteAsync(database, "UPDATE restore_probe SET value='after';");

        var restore = await service.RestoreBackupAsync(snapshot.BackupPath, backupDirectory, "BFBC");

        Assert.True(File.Exists(restore.SafetyBackupPath));
        Assert.NotEqual(snapshot.BackupPath, restore.SafetyBackupPath);
        Assert.Equal("before", await ScalarAsync(database, "SELECT value FROM restore_probe LIMIT 1;"));
        var safetyValidation = await service.ValidateBackupAsync(restore.SafetyBackupPath);
        Assert.True(safetyValidation.IsValid, safetyValidation.Message);
    }

    [Fact]
    public async Task BackupFileName_UsesOrganizationTokenAndDedicatedExtension()
    {
        using var scope = new TemporaryDirectory();
        var database = new ChurchBooksDatabase(Path.Combine(scope.Path, "live.db"));
        await database.InitializeAsync();
        var service = new ChurchBooksBackupService(database);

        var result = await service.CreateBackupAsync(Path.Combine(scope.Path, "backups"), "BFBC");

        Assert.StartsWith("ChurchBooks-BFBC-", Path.GetFileName(result.BackupPath), StringComparison.Ordinal);
        Assert.EndsWith(".cbbackup", result.BackupPath, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ExecuteAsync(ChurchBooksDatabase database, string sql)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(ChurchBooksDatabase database, string sql)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync());
    }

    private static Microsoft.Data.Sqlite.SqliteConnection CreateReadOnly(string path)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        return new Microsoft.Data.Sqlite.SqliteConnection(builder.ToString());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChurchBooks-BackupTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { }
        }
    }
}
