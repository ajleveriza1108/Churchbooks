namespace ChurchBooks.Data.Storage;

public sealed class Phase9DatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;

    public Phase9DatabaseMigrator(ChurchBooksDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new Phase8DatabaseMigrator(_database).InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
        }

        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS import_source_profiles (
                profile_id TEXT NOT NULL PRIMARY KEY,
                profile_name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                purpose TEXT NOT NULL CHECK(purpose IN ('Unknown','PeopleDirectory','Giving','BankStatement','GeneralLedger')),
                source_signature TEXT NOT NULL,
                mapping_json TEXT NOT NULL,
                default_person_role TEXT NOT NULL CHECK(default_person_role IN ('Ask','Member','Donor','Both')),
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                last_used_utc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_import_source_profiles_signature
                ON import_source_profiles(source_signature, purpose);

            CREATE TABLE IF NOT EXISTS person_import_external_links (
                profile_id TEXT NOT NULL,
                external_person_key TEXT NOT NULL COLLATE NOCASE,
                person_id TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                PRIMARY KEY(profile_id, external_person_key),
                FOREIGN KEY(profile_id) REFERENCES import_source_profiles(profile_id) ON DELETE RESTRICT,
                FOREIGN KEY(person_id) REFERENCES person_profiles(person_id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_person_import_external_links_person
                ON person_import_external_links(person_id);

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('phase','9',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value=excluded.schema_value,
                updated_utc=excluded.updated_utc;

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('schema_version','9',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value=excluded.schema_value,
                updated_utc=excluded.updated_utc;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var foreignKeyCheck = connection.CreateCommand();
        foreignKeyCheck.Transaction = transaction;
        foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await foreignKeyCheck.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Phase 9 migration failed SQLite foreign-key verification.");

        transaction.Commit();
    }
}
