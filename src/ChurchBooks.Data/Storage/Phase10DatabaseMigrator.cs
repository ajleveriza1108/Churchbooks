namespace ChurchBooks.Data.Storage;

public sealed class Phase10DatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;

    public Phase10DatabaseMigrator(ChurchBooksDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new Phase9DatabaseMigrator(_database).InitializeAsync(cancellationToken);
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
            CREATE TABLE IF NOT EXISTS vendors (
                vendor_id TEXT NOT NULL PRIMARY KEY,
                vendor_code TEXT NOT NULL UNIQUE COLLATE NOCASE,
                vendor_name TEXT NOT NULL,
                tax_id TEXT NOT NULL,
                email TEXT NOT NULL,
                phone TEXT NOT NULL,
                vendor_status TEXT NOT NULL CHECK(vendor_status IN ('Active','Archived')),
                created_utc TEXT NOT NULL,
                archived_utc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_vendors_status_name
                ON vendors(vendor_status, vendor_name);

            CREATE TABLE IF NOT EXISTS direct_expenses (
                expense_id TEXT NOT NULL PRIMARY KEY,
                vendor_id TEXT NULL,
                bank_account_id TEXT NOT NULL,
                expense_date TEXT NOT NULL,
                currency TEXT NOT NULL,
                reference TEXT NOT NULL,
                memo TEXT NOT NULL,
                journal_entry_id TEXT NOT NULL UNIQUE,
                expense_status TEXT NOT NULL CHECK(expense_status IN ('Draft','Posted')),
                total_amount TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                posted_utc TEXT NULL,
                FOREIGN KEY(vendor_id) REFERENCES vendors(vendor_id) ON DELETE RESTRICT,
                FOREIGN KEY(bank_account_id) REFERENCES bank_accounts(bank_account_id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_direct_expenses_date_status
                ON direct_expenses(expense_date, expense_status);
            CREATE INDEX IF NOT EXISTS ix_direct_expenses_vendor
                ON direct_expenses(vendor_id, expense_date);

            CREATE TABLE IF NOT EXISTS direct_expense_lines (
                expense_line_id TEXT NOT NULL PRIMARY KEY,
                expense_id TEXT NOT NULL,
                line_number INTEGER NOT NULL,
                expense_account_id TEXT NOT NULL,
                fund_id TEXT NOT NULL,
                description TEXT NOT NULL,
                amount TEXT NOT NULL,
                FOREIGN KEY(expense_id) REFERENCES direct_expenses(expense_id) ON DELETE RESTRICT,
                FOREIGN KEY(expense_account_id) REFERENCES accounts(account_id) ON DELETE RESTRICT,
                FOREIGN KEY(fund_id) REFERENCES funds(fund_id) ON DELETE RESTRICT,
                UNIQUE(expense_id, line_number)
            );

            CREATE INDEX IF NOT EXISTS ix_direct_expense_lines_account
                ON direct_expense_lines(expense_account_id, expense_id);
            CREATE INDEX IF NOT EXISTS ix_direct_expense_lines_fund
                ON direct_expense_lines(fund_id, expense_id);

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('phase','10',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value=excluded.schema_value,
                updated_utc=excluded.updated_utc;

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('schema_version','10',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
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
            throw new InvalidOperationException("Phase 10 migration failed SQLite foreign-key verification.");

        transaction.Commit();
    }
}
