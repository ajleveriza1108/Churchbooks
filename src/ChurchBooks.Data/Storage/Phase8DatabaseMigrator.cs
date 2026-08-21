namespace ChurchBooks.Data.Storage;

public sealed class Phase8DatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;

    public Phase8DatabaseMigrator(ChurchBooksDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new Phase7DatabaseMigrator(_database).InitializeAsync(cancellationToken);

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
            CREATE TABLE IF NOT EXISTS bank_statement_import_links (
                import_session_id TEXT NOT NULL PRIMARY KEY,
                bank_account_id TEXT NOT NULL,
                linked_utc TEXT NOT NULL,
                FOREIGN KEY(import_session_id) REFERENCES import_sessions(import_session_id) ON DELETE RESTRICT,
                FOREIGN KEY(bank_account_id) REFERENCES bank_accounts(bank_account_id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS bank_statement_lines (
                statement_line_id TEXT NOT NULL PRIMARY KEY,
                bank_account_id TEXT NOT NULL,
                import_session_id TEXT NOT NULL,
                transaction_date TEXT NOT NULL,
                signed_amount TEXT NOT NULL,
                description TEXT NOT NULL,
                reference TEXT NOT NULL,
                fingerprint TEXT NOT NULL,
                source_row_number INTEGER NOT NULL CHECK(source_row_number >= 2),
                created_utc TEXT NOT NULL,
                FOREIGN KEY(bank_account_id) REFERENCES bank_accounts(bank_account_id) ON DELETE RESTRICT,
                FOREIGN KEY(import_session_id) REFERENCES import_sessions(import_session_id) ON DELETE RESTRICT,
                UNIQUE(bank_account_id, fingerprint)
            );

            CREATE INDEX IF NOT EXISTS ix_bank_statement_lines_bank_date
                ON bank_statement_lines(bank_account_id, transaction_date);

            CREATE TABLE IF NOT EXISTS bank_reconciliations (
                reconciliation_id TEXT NOT NULL PRIMARY KEY,
                bank_account_id TEXT NOT NULL,
                statement_start_date TEXT NOT NULL,
                statement_end_date TEXT NOT NULL,
                statement_ending_balance TEXT NOT NULL,
                reconciliation_status TEXT NOT NULL CHECK(reconciliation_status IN ('Draft','Completed')),
                created_utc TEXT NOT NULL,
                completed_utc TEXT NULL,
                FOREIGN KEY(bank_account_id) REFERENCES bank_accounts(bank_account_id) ON DELETE RESTRICT,
                CHECK(statement_end_date >= statement_start_date)
            );

            CREATE INDEX IF NOT EXISTS ix_bank_reconciliations_bank_period
                ON bank_reconciliations(bank_account_id, statement_end_date);

            CREATE TABLE IF NOT EXISTS bank_reconciliation_match_groups (
                match_group_id TEXT NOT NULL PRIMARY KEY,
                reconciliation_id TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY(reconciliation_id) REFERENCES bank_reconciliations(reconciliation_id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS bank_reconciliation_match_statement_lines (
                match_group_id TEXT NOT NULL,
                statement_line_id TEXT NOT NULL UNIQUE,
                PRIMARY KEY(match_group_id, statement_line_id),
                FOREIGN KEY(match_group_id) REFERENCES bank_reconciliation_match_groups(match_group_id) ON DELETE CASCADE,
                FOREIGN KEY(statement_line_id) REFERENCES bank_statement_lines(statement_line_id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS bank_reconciliation_match_journals (
                match_group_id TEXT NOT NULL,
                bank_account_id TEXT NOT NULL,
                journal_entry_id TEXT NOT NULL,
                PRIMARY KEY(match_group_id, journal_entry_id),
                FOREIGN KEY(match_group_id) REFERENCES bank_reconciliation_match_groups(match_group_id) ON DELETE CASCADE,
                FOREIGN KEY(bank_account_id) REFERENCES bank_accounts(bank_account_id) ON DELETE RESTRICT,
                FOREIGN KEY(journal_entry_id) REFERENCES journal_entries(journal_entry_id) ON DELETE RESTRICT,
                UNIQUE(bank_account_id, journal_entry_id)
            );

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('phase','8',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value=excluded.schema_value,
                updated_utc=excluded.updated_utc;

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('schema_version','8',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
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
            throw new InvalidOperationException("Phase 8 migration failed SQLite foreign-key verification.");

        transaction.Commit();
    }
}
