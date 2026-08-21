using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class Phase6DatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;
    public Phase6DatabaseMigrator(ChurchBooksDatabase database) => _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new OfferingDatabaseMigrator(_database).InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS organization_profile (
                profile_key TEXT NOT NULL PRIMARY KEY CHECK(profile_key='PRIMARY'),
                display_name TEXT NOT NULL,
                legal_name TEXT NOT NULL,
                base_currency TEXT NOT NULL,
                fiscal_year_start_month INTEGER NOT NULL CHECK(fiscal_year_start_month BETWEEN 1 AND 12),
                country_code TEXT NOT NULL,
                tax_identifier TEXT NOT NULL,
                setup_complete INTEGER NOT NULL CHECK(setup_complete IN (0,1)),
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS terminology_overrides (
                term_key TEXT NOT NULL PRIMARY KEY,
                singular_label TEXT NOT NULL,
                plural_label TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS custom_search_aliases (
                alias_id TEXT NOT NULL PRIMARY KEY,
                area_key TEXT NOT NULL,
                alias_text TEXT NOT NULL UNIQUE COLLATE NOCASE,
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS bank_accounts (
                bank_account_id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                institution_name TEXT NOT NULL,
                account_last_four TEXT NOT NULL,
                currency_code TEXT NOT NULL,
                ledger_account_id TEXT NOT NULL UNIQUE,
                bank_status TEXT NOT NULL CHECK(bank_status IN ('Active','Archived')),
                created_utc TEXT NOT NULL,
                archived_utc TEXT NULL,
                FOREIGN KEY(ledger_account_id) REFERENCES accounts(account_id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS giving_category_income_accounts (
                giving_category_id TEXT NOT NULL PRIMARY KEY,
                income_account_id TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY(giving_category_id) REFERENCES giving_categories(giving_category_id) ON DELETE RESTRICT,
                FOREIGN KEY(income_account_id) REFERENCES accounts(account_id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS bank_deposits (
                deposit_id TEXT NOT NULL PRIMARY KEY,
                bank_account_id TEXT NOT NULL,
                deposit_date TEXT NOT NULL,
                currency_code TEXT NOT NULL,
                total_amount TEXT NOT NULL,
                reference TEXT NOT NULL,
                memo TEXT NOT NULL,
                journal_entry_id TEXT NOT NULL UNIQUE,
                deposit_status TEXT NOT NULL CHECK(deposit_status IN ('Draft','Posted')),
                created_utc TEXT NOT NULL,
                posted_utc TEXT NULL,
                FOREIGN KEY(bank_account_id) REFERENCES bank_accounts(bank_account_id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS bank_deposit_contributions (
                deposit_id TEXT NOT NULL,
                contribution_id TEXT NOT NULL UNIQUE,
                amount TEXT NOT NULL,
                PRIMARY KEY(deposit_id, contribution_id),
                FOREIGN KEY(deposit_id) REFERENCES bank_deposits(deposit_id) ON DELETE RESTRICT,
                FOREIGN KEY(contribution_id) REFERENCES contributions(contribution_id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_bank_deposits_account_date ON bank_deposits(bank_account_id, deposit_date);
            CREATE INDEX IF NOT EXISTS ix_bank_deposit_contributions_deposit ON bank_deposit_contributions(deposit_id);

            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('phase','6',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET schema_value=excluded.schema_value, updated_utc=excluded.updated_utc;
            INSERT INTO app_schema(schema_key, schema_value, updated_utc)
            VALUES('schema_version','6',strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT(schema_key) DO UPDATE SET schema_value=excluded.schema_value, updated_utc=excluded.updated_utc;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }
}
