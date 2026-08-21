using System.Globalization;
using System.Text.Json;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Importing;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqliteSmartImportStore : ISmartImportStore
{
    private readonly ChurchBooksDatabase _database;

    public SqliteSmartImportStore(ChurchBooksDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task<ImportMappingTemplate?> FindTemplateAsync(
        string sourceSignature,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT template_id, template_name, source_signature, mapping_json
            FROM import_mapping_templates
            WHERE source_signature = $signature;
            """;
        command.Parameters.AddWithValue("$signature", sourceSignature);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadTemplate(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))
            : null;
    }

    public async Task<IReadOnlyList<ImportMappingTemplate>> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = new List<ImportMappingTemplate>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT template_id, template_name, source_signature, mapping_json
            FROM import_mapping_templates
            ORDER BY template_name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadTemplate(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        return items;
    }

    public async Task SaveTemplateAsync(
        ImportMappingTemplate template,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO import_mapping_templates(
                template_id, template_name, source_signature, mapping_json, created_utc, updated_utc)
            VALUES(
                $id, $name, $signature, $json,
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(source_signature) DO UPDATE SET
                template_name=excluded.template_name,
                mapping_json=excluded.mapping_json,
                updated_utc=excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$id", template.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", template.Name);
        command.Parameters.AddWithValue("$signature", template.SourceSignature);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(template.Mappings));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
        IEnumerable<string> fingerprints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);
        var requested = fingerprints
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requested.Length == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = await OpenAsync(cancellationToken);
        const int batchSize = 400;
        for (var offset = 0; offset < requested.Length; offset += batchSize)
        {
            var batch = requested.Skip(offset).Take(batchSize).ToArray();
            await using var command = connection.CreateCommand();
            var placeholders = new string[batch.Length];
            for (var index = 0; index < batch.Length; index++)
            {
                var parameterName = "$fingerprint" + index;
                placeholders[index] = parameterName;
                command.Parameters.AddWithValue(parameterName, batch[index]);
            }

            command.CommandText =
                "SELECT DISTINCT fingerprint FROM import_staged_rows WHERE fingerprint IN (" +
                string.Join(",", placeholders) +
                ");";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                found.Add(reader.GetString(0));
            }
        }

        return found;
    }

    public async Task StageSessionAsync(ImportSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO import_sessions(
                    import_session_id, file_name, file_hash, worksheet_name, source_kind,
                    source_row_count, staged_row_count, duplicate_count, session_status, created_utc)
                VALUES(
                    $id, $file, $hash, $sheet, $kind,
                    $sourceCount, $stagedCount, $duplicates, 'Staged',
                    strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.Parameters.AddWithValue("$id", session.Id.ToString("D"));
            command.Parameters.AddWithValue("$file", session.FileName);
            command.Parameters.AddWithValue("$hash", session.FileHash);
            command.Parameters.AddWithValue("$sheet", session.WorksheetName);
            command.Parameters.AddWithValue("$kind", session.SourceKind.ToString());
            command.Parameters.AddWithValue("$sourceCount", session.SourceRowCount);
            command.Parameters.AddWithValue("$stagedCount", session.Rows.Count);
            command.Parameters.AddWithValue("$duplicates", session.DuplicateCount);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var row in session.Rows)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO import_staged_rows(
                    staged_row_id, import_session_id, source_row_number, fingerprint,
                    normalized_json, potential_duplicate, created_utc)
                VALUES(
                    $id, $session, $row, $fingerprint,
                    $json, $duplicate, strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.Parameters.AddWithValue("$id", row.Id.ToString("D"));
            command.Parameters.AddWithValue("$session", session.Id.ToString("D"));
            command.Parameters.AddWithValue("$row", row.SourceRowNumber);
            command.Parameters.AddWithValue("$fingerprint", row.Fingerprint);
            command.Parameters.AddWithValue("$json", row.NormalizedJson);
            command.Parameters.AddWithValue("$duplicate", row.IsPotentialDuplicate ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<ImportSession>> GetSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var headers = new List<SessionHeader>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = SessionSelect + " ORDER BY created_utc DESC;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                headers.Add(ReadSessionHeader(reader));
            }
        }

        var sessions = new List<ImportSession>(headers.Count);
        foreach (var header in headers)
        {
            sessions.Add(await LoadSessionAsync(connection, header, cancellationToken));
        }
        return sessions;
    }

    public async Task<ImportSession?> GetSessionAsync(
        Guid importSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        SessionHeader? header;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = SessionSelect + " WHERE import_session_id = $id;";
            command.Parameters.AddWithValue("$id", importSessionId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            header = await reader.ReadAsync(cancellationToken) ? ReadSessionHeader(reader) : null;
        }

        return header is null
            ? null
            : await LoadSessionAsync(connection, header, cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task<ImportSession> LoadSessionAsync(
        SqliteConnection connection,
        SessionHeader header,
        CancellationToken cancellationToken)
    {
        var rows = new List<ImportStagedRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT staged_row_id, source_row_number, fingerprint, normalized_json, potential_duplicate
            FROM import_staged_rows
            WHERE import_session_id = $session
            ORDER BY source_row_number;
            """;
        command.Parameters.AddWithValue("$session", header.Id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ImportStagedRow(
                Guid.Parse(reader.GetString(0)),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4) != 0));
        }

        return new ImportSession(
            header.Id,
            header.FileName,
            header.FileHash,
            header.WorksheetName,
            header.SourceKind,
            header.SourceRowCount,
            rows);
    }

    private static SessionHeader ReadSessionHeader(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        Enum.Parse<ImportSourceKind>(reader.GetString(4), ignoreCase: false),
        reader.GetInt32(5));

    private static ImportMappingTemplate ReadTemplate(
        string id,
        string name,
        string signature,
        string json) =>
        new(
            Guid.Parse(id),
            name,
            signature,
            JsonSerializer.Deserialize<ImportColumnMapping[]>(json) ?? Array.Empty<ImportColumnMapping>());

    private const string SessionSelect = """
        SELECT import_session_id, file_name, file_hash, worksheet_name, source_kind, source_row_count
        FROM import_sessions
        """;

    private sealed record SessionHeader(
        Guid Id,
        string FileName,
        string FileHash,
        string WorksheetName,
        ImportSourceKind SourceKind,
        int SourceRowCount);
}
