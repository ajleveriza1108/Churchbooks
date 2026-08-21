namespace ChurchBooks.Data.Storage;

public sealed class OfferingDatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;

    public OfferingDatabaseMigrator(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new PeopleGivingDatabaseMigrator(_database).InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
        }

        using var transaction = connection.BeginTransaction();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                PRAGMA defer_foreign_keys = ON;

                INSERT INTO schema_migrations(migration_key, schema_version, description, applied_utc)
                VALUES ('005_individual_offerings_analytics', 5, 'Offering batches individual contribution breakdowns and giving analytics', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(migration_key) DO NOTHING;

                CREATE TABLE IF NOT EXISTS offering_batches (
                    offering_batch_id TEXT NOT NULL PRIMARY KEY,
                    service_date TEXT NOT NULL,
                    name TEXT NOT NULL,
                    reference TEXT NOT NULL,
                    batch_status TEXT NOT NULL CHECK (batch_status IN ('Open', 'Closed')),
                    created_utc TEXT NOT NULL,
                    closed_utc TEXT NULL,
                    CHECK ((batch_status = 'Open' AND closed_utc IS NULL) OR (batch_status = 'Closed' AND closed_utc IS NOT NULL))
                );

                CREATE INDEX IF NOT EXISTS ix_offering_batches_date_status
                    ON offering_batches(service_date, batch_status, name);

                CREATE TABLE IF NOT EXISTS contributions (
                    contribution_id TEXT NOT NULL PRIMARY KEY,
                    offering_batch_id TEXT NOT NULL,
                    person_id TEXT NOT NULL,
                    received_date TEXT NOT NULL,
                    currency_code TEXT NOT NULL CHECK (length(currency_code) = 3),
                    total_amount TEXT NOT NULL,
                    reference TEXT NOT NULL,
                    memo TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    FOREIGN KEY (offering_batch_id) REFERENCES offering_batches(offering_batch_id) ON DELETE RESTRICT,
                    FOREIGN KEY (person_id) REFERENCES person_profiles(person_id) ON DELETE RESTRICT
                );

                CREATE INDEX IF NOT EXISTS ix_contributions_person_date
                    ON contributions(person_id, received_date, contribution_id);

                CREATE INDEX IF NOT EXISTS ix_contributions_batch
                    ON contributions(offering_batch_id, person_id, contribution_id);

                CREATE TABLE IF NOT EXISTS contribution_lines (
                    contribution_line_id TEXT NOT NULL PRIMARY KEY,
                    contribution_id TEXT NOT NULL,
                    giving_category_id TEXT NOT NULL,
                    fund_id TEXT NOT NULL,
                    amount TEXT NOT NULL,
                    FOREIGN KEY (contribution_id) REFERENCES contributions(contribution_id) ON DELETE RESTRICT,
                    FOREIGN KEY (giving_category_id) REFERENCES giving_categories(giving_category_id) ON DELETE RESTRICT,
                    FOREIGN KEY (fund_id) REFERENCES funds(fund_id) ON DELETE RESTRICT,
                    UNIQUE (contribution_id, giving_category_id, fund_id)
                );

                CREATE INDEX IF NOT EXISTS ix_contribution_lines_category
                    ON contribution_lines(giving_category_id, contribution_id);

                CREATE INDEX IF NOT EXISTS ix_contribution_lines_fund
                    ON contribution_lines(fund_id, contribution_id);

                -- ChurchBooks provides only Tithes as a starter giving category.
                -- It remains an ordinary user-managed record: it may be renamed or archived later.
                -- Other giving categories and all ministry/designation names are created by the user.
                INSERT OR IGNORE INTO giving_categories(
                    giving_category_id, code, name, description, group_name,
                    category_status, created_utc, archived_utc)
                VALUES (
                    'd8cb9944-f3e1-4ff0-9fbe-7ee9eb521001', 'TITHE', 'Tithes',
                    'Starter giving category. Rename or remove from active use when your church prefers different terminology.',
                    'Core Giving', 'Active', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), NULL);

                -- Phase 5 R1 stored the starter category as 32 compact hex characters.
                -- Normalize that legacy storage key to canonical Guid "D" format transactionally.
                -- Child references are updated in the same deferred-FK transaction so history is preserved.
                UPDATE contribution_lines
                SET giving_category_id =
                    substr(giving_category_id, 1, 8) || '-' ||
                    substr(giving_category_id, 9, 4) || '-' ||
                    substr(giving_category_id, 13, 4) || '-' ||
                    substr(giving_category_id, 17, 4) || '-' ||
                    substr(giving_category_id, 21, 12)
                WHERE giving_category_id IN (
                    SELECT giving_category_id
                    FROM giving_categories
                    WHERE code = 'TITHE'
                      AND length(giving_category_id) = 32
                      AND instr(giving_category_id, '-') = 0
                );

                UPDATE giving_categories
                SET giving_category_id =
                    substr(giving_category_id, 1, 8) || '-' ||
                    substr(giving_category_id, 9, 4) || '-' ||
                    substr(giving_category_id, 13, 4) || '-' ||
                    substr(giving_category_id, 17, 4) || '-' ||
                    substr(giving_category_id, 21, 12)
                WHERE code = 'TITHE'
                  AND length(giving_category_id) = 32
                  AND instr(giving_category_id, '-') = 0;

                INSERT INTO app_schema(schema_key, schema_value, updated_utc)
                VALUES ('phase', '5', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(schema_key) DO UPDATE SET schema_value = excluded.schema_value, updated_utc = excluded.updated_utc;

                INSERT INTO app_schema(schema_key, schema_value, updated_utc)
                VALUES ('schema_version', '5', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(schema_key) DO UPDATE SET schema_value = excluded.schema_value, updated_utc = excluded.updated_utc;

                INSERT INTO app_schema(schema_key, schema_value, updated_utc)
                VALUES ('individual_offerings_analytics', '1', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(schema_key) DO UPDATE SET schema_value = excluded.schema_value, updated_utc = excluded.updated_utc;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var verify = connection.CreateCommand())
        {
            verify.Transaction = transaction;
            verify.CommandText = "SELECT schema_version FROM schema_migrations WHERE migration_key = '005_individual_offerings_analytics';";
            var value = await verify.ExecuteScalarAsync(cancellationToken);
            if (Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 5)
                throw new InvalidOperationException("Schema migration 005_individual_offerings_analytics was not recorded as version 5.");
        }
        await using (var foreignKeyCheck = connection.CreateCommand())
        {
            foreignKeyCheck.Transaction = transaction;
            foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await foreignKeyCheck.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Schema v5 migration left a foreign-key violation and was rolled back.");
            }
        }

        transaction.Commit();
    }
}
