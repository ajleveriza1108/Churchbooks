namespace ChurchBooks.Data.Storage;

public sealed class Phase7DatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;
    public Phase7DatabaseMigrator(ChurchBooksDatabase database) => _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new Phase6DatabaseMigrator(_database).InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS import_mapping_templates (
                template_id TEXT NOT NULL PRIMARY KEY,
                template_name TEXT NOT NULL,
                source_signature TEXT NOT NULL UNIQUE COLLATE NOCASE,
                mapping_json TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS import_sessions (
                import_session_id TEXT NOT NULL PRIMARY KEY,
                file_name TEXT NOT NULL,
                file_hash TEXT NOT NULL,
                worksheet_name TEXT NOT NULL,
                source_kind TEXT NOT NULL CHECK(source_kind IN ('Csv','Xls','Xlsx')),
                source_row_count INTEGER NOT NULL CHECK(source_row_count >= 0),
                staged_row_count INTEGER NOT NULL CHECK(staged_row_count >= 0),
                duplicate_count INTEGER NOT NULL CHECK(duplicate_count >= 0),
                session_status TEXT NOT NULL CHECK(session_status IN ('Staged')),
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS import_staged_rows (
                staged_row_id TEXT NOT NULL PRIMARY KEY,
                import_session_id TEXT NOT NULL,
                source_row_number INTEGER NOT NULL CHECK(source_row_number >= 2),
                fingerprint TEXT NOT NULL,
                normalized_json TEXT NOT NULL,
                potential_duplicate INTEGER NOT NULL CHECK(potential_duplicate IN (0,1)),
                created_utc TEXT NOT NULL,
                FOREIGN KEY(import_session_id) REFERENCES import_sessions(import_session_id) ON DELETE RESTRICT,
                UNIQUE(import_session_id, source_row_number)
            );

            CREATE INDEX IF NOT EXISTS ix_import_staged_rows_fingerprint ON import_staged_rows(fingerprint);
            CREATE INDEX IF NOT EXISTS ix_import_sessions_hash ON import_sessions(file_hash);

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('phase','7',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET schema_value=excluded.schema_value, updated_utc=excluded.updated_utc;
            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('schema_version','7',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET schema_value=excluded.schema_value, updated_utc=excluded.updated_utc;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }
}
