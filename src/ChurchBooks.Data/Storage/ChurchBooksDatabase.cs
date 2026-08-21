using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class ChurchBooksDatabase
{
    public string DatabasePath { get; }

    public ChurchBooksDatabase(string? databasePath = null)
    {
        DatabasePath = databasePath ?? GetDefaultDatabasePath();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(DatabasePath)
            ?? throw new InvalidOperationException("The database path has no parent directory.");

        Directory.CreateDirectory(directory);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);

        const string bootstrapSql = """
            CREATE TABLE IF NOT EXISTS app_schema (
                schema_key TEXT NOT NULL PRIMARY KEY,
                schema_value TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS accounts (
                account_id TEXT NOT NULL PRIMARY KEY,
                code TEXT NOT NULL UNIQUE COLLATE NOCASE,
                name TEXT NOT NULL,
                account_type TEXT NOT NULL CHECK (account_type IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense')),
                account_status TEXT NOT NULL CHECK (account_status IN ('Active', 'Inactive')),
                parent_account_id TEXT NULL,
                allow_direct_posting INTEGER NOT NULL CHECK (allow_direct_posting IN (0, 1)),
                created_utc TEXT NOT NULL,
                FOREIGN KEY (parent_account_id) REFERENCES accounts(account_id)
            );

            CREATE INDEX IF NOT EXISTS ix_accounts_type_status
                ON accounts(account_type, account_status);

            CREATE TABLE IF NOT EXISTS accounting_periods (
                period_id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                start_date TEXT NOT NULL,
                end_date TEXT NOT NULL,
                period_status TEXT NOT NULL CHECK (period_status IN ('Open', 'Closed')),
                created_utc TEXT NOT NULL,
                CHECK (end_date >= start_date)
            );

            CREATE INDEX IF NOT EXISTS ix_accounting_periods_dates
                ON accounting_periods(start_date, end_date);

            CREATE TABLE IF NOT EXISTS journal_entries (
                journal_entry_id TEXT NOT NULL PRIMARY KEY,
                entry_number TEXT NOT NULL UNIQUE COLLATE NOCASE,
                period_id TEXT NOT NULL,
                posting_date TEXT NOT NULL,
                description TEXT NOT NULL,
                reference TEXT NOT NULL,
                base_currency TEXT NOT NULL,
                total_debit TEXT NOT NULL,
                total_credit TEXT NOT NULL,
                posted_utc TEXT NOT NULL,
                FOREIGN KEY (period_id) REFERENCES accounting_periods(period_id),
                CHECK (total_debit = total_credit)
            );

            CREATE INDEX IF NOT EXISTS ix_journal_entries_period_date
                ON journal_entries(period_id, posting_date, entry_number);

            CREATE TABLE IF NOT EXISTS journal_lines (
                journal_line_id TEXT NOT NULL PRIMARY KEY,
                journal_entry_id TEXT NOT NULL,
                line_number INTEGER NOT NULL,
                account_id TEXT NOT NULL,
                debit TEXT NOT NULL,
                credit TEXT NOT NULL,
                memo TEXT NOT NULL,
                FOREIGN KEY (journal_entry_id) REFERENCES journal_entries(journal_entry_id) ON DELETE RESTRICT,
                FOREIGN KEY (account_id) REFERENCES accounts(account_id) ON DELETE RESTRICT,
                UNIQUE (journal_entry_id, line_number)
            );

            CREATE INDEX IF NOT EXISTS ix_journal_lines_account_entry
                ON journal_lines(account_id, journal_entry_id);

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES ('phase', '2', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value = excluded.schema_value,
                updated_utc = excluded.updated_utc;

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES ('accounting_kernel', '1', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(schema_key) DO UPDATE SET
                schema_value = excluded.schema_value,
                updated_utc = excluded.updated_utc;
            """;

        await ExecuteAsync(connection, bootstrapSql, cancellationToken);
    }

    public SqliteConnection CreateConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = false
        }.ToString();

        return new SqliteConnection(connectionString);
    }

    public static string GetDefaultDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "ChurchBooks", "Data", "ChurchBooks.db");
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
