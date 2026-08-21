namespace ChurchBooks.Data.Storage;

public sealed class FundAccountingDatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;

    public FundAccountingDatabaseMigrator(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);

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
            CREATE TABLE IF NOT EXISTS schema_migrations (
                migration_key TEXT NOT NULL PRIMARY KEY,
                schema_version INTEGER NOT NULL UNIQUE,
                description TEXT NOT NULL,
                applied_utc TEXT NOT NULL
            );

            INSERT INTO schema_migrations(migration_key, schema_version, description, applied_utc)
            VALUES ('001_foundation', 1, 'Windows foundation and local SQLite bootstrap', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(migration_key) DO NOTHING;

            INSERT INTO schema_migrations(migration_key, schema_version, description, applied_utc)
            VALUES ('002_accounting_kernel', 2, 'Double-entry accounting kernel', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(migration_key) DO NOTHING;

            INSERT INTO schema_migrations(migration_key, schema_version, description, applied_utc)
            VALUES ('003_fund_accounting', 3, 'Native church fund accounting dimension', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(migration_key) DO NOTHING;

            CREATE TABLE IF NOT EXISTS funds (
                fund_id TEXT NOT NULL PRIMARY KEY,
                code TEXT NOT NULL UNIQUE COLLATE NOCASE,
                name TEXT NOT NULL,
                restriction_class TEXT NOT NULL CHECK (restriction_class IN ('Unrestricted', 'BoardDesignated', 'DonorRestricted', 'Endowment')),
                overspend_policy TEXT NOT NULL CHECK (overspend_policy IN ('Allow', 'Warn', 'Block')),
                purpose TEXT NOT NULL,
                fund_status TEXT NOT NULL CHECK (fund_status IN ('Active', 'Archived')),
                created_utc TEXT NOT NULL,
                archived_utc TEXT NULL,
                CHECK ((fund_status = 'Active' AND archived_utc IS NULL) OR (fund_status = 'Archived' AND archived_utc IS NOT NULL))
            );

            CREATE INDEX IF NOT EXISTS ix_funds_status_restriction
                ON funds(fund_status, restriction_class, code);

            CREATE TABLE IF NOT EXISTS journal_line_funds (
                journal_line_id TEXT NOT NULL PRIMARY KEY,
                fund_id TEXT NOT NULL,
                assigned_utc TEXT NOT NULL,
                FOREIGN KEY (journal_line_id) REFERENCES journal_lines(journal_line_id) ON DELETE RESTRICT,
                FOREIGN KEY (fund_id) REFERENCES funds(fund_id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_journal_line_funds_fund_line
                ON journal_line_funds(fund_id, journal_line_id);

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES ('phase', '3B', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value = excluded.schema_value,
                updated_utc = excluded.updated_utc;

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES ('schema_version', '3', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value = excluded.schema_value,
                updated_utc = excluded.updated_utc;

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES ('fund_accounting', '1', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value = excluded.schema_value,
                updated_utc = excluded.updated_utc;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var expectedMigrations = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["001_foundation"] = 1,
            ["002_accounting_kernel"] = 2,
            ["003_fund_accounting"] = 3
        };
        var verifiedMigrations = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var verifyCommand = connection.CreateCommand())
        {
            verifyCommand.Transaction = transaction;
            verifyCommand.CommandText = """
                SELECT migration_key, schema_version
                FROM schema_migrations
                WHERE migration_key IN ('001_foundation', '002_accounting_kernel', '003_fund_accounting');
                """;
            await using var reader = await verifyCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                verifiedMigrations[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        foreach (var expected in expectedMigrations)
        {
            if (!verifiedMigrations.TryGetValue(expected.Key, out var actualVersion) || actualVersion != expected.Value)
            {
                throw new InvalidOperationException($"Schema migration history conflict for {expected.Key}. Expected version {expected.Value}.");
            }
        }

        transaction.Commit();
    }
}
