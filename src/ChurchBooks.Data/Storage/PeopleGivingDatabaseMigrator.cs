namespace ChurchBooks.Data.Storage;

public sealed class PeopleGivingDatabaseMigrator
{
    private readonly ChurchBooksDatabase _database;

    public PeopleGivingDatabaseMigrator(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new FundAccountingDatabaseMigrator(_database).InitializeAsync(cancellationToken);

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
                INSERT INTO schema_migrations(migration_key, schema_version, description, applied_utc)
                VALUES ('004_people_giving_directory', 4, 'Members donors households and giving categories', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(migration_key) DO NOTHING;

                CREATE TABLE IF NOT EXISTS households (
                    household_id TEXT NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    statement_name TEXT NOT NULL,
                    household_status TEXT NOT NULL CHECK (household_status IN ('Active', 'Archived')),
                    created_utc TEXT NOT NULL,
                    archived_utc TEXT NULL,
                    CHECK ((household_status = 'Active' AND archived_utc IS NULL) OR (household_status = 'Archived' AND archived_utc IS NOT NULL))
                );

                CREATE INDEX IF NOT EXISTS ix_households_status_name
                    ON households(household_status, name);

                CREATE TABLE IF NOT EXISTS person_profiles (
                    person_id TEXT NOT NULL PRIMARY KEY,
                    first_name TEXT NOT NULL,
                    middle_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    preferred_name TEXT NOT NULL,
                    email TEXT NOT NULL,
                    phone TEXT NOT NULL,
                    member_number TEXT NOT NULL COLLATE NOCASE,
                    is_member INTEGER NOT NULL CHECK (is_member IN (0, 1)),
                    is_donor INTEGER NOT NULL CHECK (is_donor IN (0, 1)),
                    household_id TEXT NULL,
                    person_status TEXT NOT NULL CHECK (person_status IN ('Active', 'Archived')),
                    created_utc TEXT NOT NULL,
                    archived_utc TEXT NULL,
                    CHECK (is_member = 1 OR is_donor = 1),
                    CHECK ((person_status = 'Active' AND archived_utc IS NULL) OR (person_status = 'Archived' AND archived_utc IS NOT NULL)),
                    FOREIGN KEY (household_id) REFERENCES households(household_id) ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ux_person_profiles_member_number
                    ON person_profiles(member_number COLLATE NOCASE)
                    WHERE member_number <> '';

                CREATE INDEX IF NOT EXISTS ix_person_profiles_status_name
                    ON person_profiles(person_status, last_name, first_name);

                CREATE INDEX IF NOT EXISTS ix_person_profiles_household
                    ON person_profiles(household_id, person_status);

                CREATE TABLE IF NOT EXISTS giving_categories (
                    giving_category_id TEXT NOT NULL PRIMARY KEY,
                    code TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    description TEXT NOT NULL,
                    group_name TEXT NOT NULL,
                    category_status TEXT NOT NULL CHECK (category_status IN ('Active', 'Archived')),
                    created_utc TEXT NOT NULL,
                    archived_utc TEXT NULL,
                    CHECK ((category_status = 'Active' AND archived_utc IS NULL) OR (category_status = 'Archived' AND archived_utc IS NOT NULL))
                );

                CREATE INDEX IF NOT EXISTS ix_giving_categories_status_group
                    ON giving_categories(category_status, group_name, name);

                INSERT INTO app_schema(schema_key, schema_value, updated_utc)
                VALUES ('phase', '4', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(schema_key) DO UPDATE SET
                    schema_value = excluded.schema_value,
                    updated_utc = excluded.updated_utc;

                INSERT INTO app_schema(schema_key, schema_value, updated_utc)
                VALUES ('schema_version', '4', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(schema_key) DO UPDATE SET
                    schema_value = excluded.schema_value,
                    updated_utc = excluded.updated_utc;

                INSERT INTO app_schema(schema_key, schema_value, updated_utc)
                VALUES ('people_giving_directory', '1', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(schema_key) DO UPDATE SET
                    schema_value = excluded.schema_value,
                    updated_utc = excluded.updated_utc;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var expectedMigrations = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["001_foundation"] = 1,
            ["002_accounting_kernel"] = 2,
            ["003_fund_accounting"] = 3,
            ["004_people_giving_directory"] = 4
        };
        var verifiedMigrations = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var verifyCommand = connection.CreateCommand())
        {
            verifyCommand.Transaction = transaction;
            verifyCommand.CommandText = """
                SELECT migration_key, schema_version
                FROM schema_migrations
                WHERE migration_key IN ('001_foundation', '002_accounting_kernel', '003_fund_accounting', '004_people_giving_directory');
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
