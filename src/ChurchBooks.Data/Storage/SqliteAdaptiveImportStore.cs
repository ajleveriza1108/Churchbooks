using System.Globalization;
using System.Text.Json;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Importing;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteAdaptiveImportStore : IAdaptiveImportStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteAdaptiveImportStore(ChurchBooksDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task<IReadOnlyList<ImportSourceProfile>> GetSourceProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ProfileSelect + " ORDER BY CASE WHEN last_used_utc IS NULL THEN 1 ELSE 0 END, last_used_utc DESC, profile_name COLLATE NOCASE;";
        return await ReadProfilesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<ImportSourceProfile>> FindSourceProfilesAsync(string sourceSignature, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSignature);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ProfileSelect + " WHERE source_signature=$signature COLLATE NOCASE ORDER BY CASE WHEN last_used_utc IS NULL THEN 1 ELSE 0 END, last_used_utc DESC, profile_name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$signature", sourceSignature.Trim());
        return await ReadProfilesAsync(command, cancellationToken);
    }

    public async Task SaveSourceProfileAsync(ImportSourceProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var mappingJson = JsonSerializer.Serialize(profile.Mappings.Select(x => new MappingDto(x.ColumnIndex, x.Header, x.Role)).ToArray());
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO import_source_profiles(
                profile_id, profile_name, purpose, source_signature, mapping_json,
                default_person_role, created_utc, updated_utc, last_used_utc)
            VALUES($id,$name,$purpose,$signature,$mapping,$role,$now,$now,$last)
            ON CONFLICT(profile_id) DO UPDATE SET
                profile_name=excluded.profile_name,
                purpose=excluded.purpose,
                source_signature=excluded.source_signature,
                mapping_json=excluded.mapping_json,
                default_person_role=excluded.default_person_role,
                updated_utc=excluded.updated_utc,
                last_used_utc=excluded.last_used_utc;
            """;
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        command.Parameters.AddWithValue("$id", profile.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$purpose", profile.Purpose.ToString());
        command.Parameters.AddWithValue("$signature", profile.SourceSignature);
        command.Parameters.AddWithValue("$mapping", mappingJson);
        command.Parameters.AddWithValue("$role", profile.DefaultPersonRole.ToString());
        command.Parameters.AddWithValue("$now", now);
        command.Parameters.AddWithValue("$last", profile.LastUsedUtc.HasValue ? profile.LastUsedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TouchSourceProfileAsync(Guid profileId, DateTimeOffset usedUtc, CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty) throw new ArgumentException("Source profile ID is required.", nameof(profileId));
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE import_source_profiles SET last_used_utc=$used, updated_utc=$used WHERE profile_id=$id;";
        command.Parameters.AddWithValue("$id", profileId.ToString("D"));
        command.Parameters.AddWithValue("$used", usedUtc.ToString("O", CultureInfo.InvariantCulture));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("The selected source profile no longer exists.");
    }

    public async Task<Guid?> FindLinkedPersonIdAsync(Guid profileId, string externalPersonKey, CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty || string.IsNullOrWhiteSpace(externalPersonKey)) return null;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT person_id FROM person_import_external_links WHERE profile_id=$profile AND external_person_key=$key COLLATE NOCASE;";
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$key", externalPersonKey.Trim());
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is string value ? Guid.Parse(value) : null;
    }

    public async Task SavePersonExternalLinkAsync(Guid profileId, string externalPersonKey, Guid personId, CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty) throw new ArgumentException("Source profile ID is required.", nameof(profileId));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalPersonKey);
        if (personId == Guid.Empty) throw new ArgumentException("Person ID is required.", nameof(personId));
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT person_id FROM person_import_external_links WHERE profile_id=$profile AND external_person_key=$key COLLATE NOCASE;";
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$key", externalPersonKey.Trim());
        var current = await command.ExecuteScalarAsync(cancellationToken);
        if (current is string currentId && Guid.Parse(currentId) != personId)
            throw new InvalidOperationException("This source person ID is already linked to a different ChurchBooks person. Review the identity instead of reassigning it automatically.");
        if (current is not null) return;
        command.Parameters.Clear();
        command.CommandText = "INSERT INTO person_import_external_links(profile_id, external_person_key, person_id, created_utc) VALUES($profile,$key,$person,$created);";
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$key", externalPersonKey.Trim());
        command.Parameters.AddWithValue("$person", personId.ToString("D"));
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task<IReadOnlyList<ImportSourceProfile>> ReadProfilesAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var result = new List<ImportSourceProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var mappings = JsonSerializer.Deserialize<MappingDto[]>(reader.GetString(4)) ?? Array.Empty<MappingDto>();
            result.Add(new ImportSourceProfile(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                Enum.Parse<ImportPurpose>(reader.GetString(2), ignoreCase: false),
                reader.GetString(3),
                mappings.Select(x => new ImportColumnMapping(x.ColumnIndex, x.Header, x.Role)),
                Enum.Parse<PersonImportDefaultRole>(reader.GetString(5), ignoreCase: false),
                reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return result;
    }

    private const string ProfileSelect = "SELECT profile_id, profile_name, purpose, source_signature, mapping_json, default_person_role, last_used_utc FROM import_source_profiles";
    private sealed record MappingDto(int ColumnIndex, string Header, ImportColumnRole Role);
}
