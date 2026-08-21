using System.Data.Common;
using System.Globalization;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.People;
using Microsoft.Data.Sqlite;

namespace ChurchBooks.Data.Storage;

public sealed class SqlitePeopleGivingStore : IPeopleGivingStore
{
    private readonly ChurchBooksDatabase _database;

    public SqlitePeopleGivingStore(ChurchBooksDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task AddPersonAsync(PersonProfile person, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(person);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO person_profiles(
                person_id, first_name, middle_name, last_name, preferred_name, email, phone,
                member_number, is_member, is_donor, household_id, person_status, created_utc, archived_utc)
            VALUES (
                $person_id, $first_name, $middle_name, $last_name, $preferred_name, $email, $phone,
                $member_number, $is_member, $is_donor, $household_id, $person_status, $created_utc, $archived_utc);
            """;
        BindPerson(command, person, includeCreatedUtc: true);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdatePersonAsync(PersonProfile person, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(person);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE person_profiles SET
                first_name = $first_name,
                middle_name = $middle_name,
                last_name = $last_name,
                preferred_name = $preferred_name,
                email = $email,
                phone = $phone,
                member_number = $member_number,
                is_member = $is_member,
                is_donor = $is_donor,
                household_id = $household_id,
                person_status = $person_status,
                archived_utc = $archived_utc
            WHERE person_id = $person_id;
            """;
        BindPerson(command, person, includeCreatedUtc: false);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The person could not be updated because it no longer exists.");
        }
    }

    public async Task DeletePersonAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM person_profiles WHERE person_id = $person_id;";
        command.Parameters.AddWithValue("$person_id", personId.ToString("D"));
        try
        {
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0) throw new InvalidOperationException("The selected person no longer exists.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("The selected person is referenced by historical giving or import records.", ex);
        }
    }

    public async Task<PersonProfile?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        if (personId == Guid.Empty)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PersonSelect + " WHERE person_id = $person_id;";
        command.Parameters.AddWithValue("$person_id", personId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPerson(reader) : null;
    }

    public async Task<IReadOnlyList<PersonProfile>> GetPeopleAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PersonSelect + (includeArchived
            ? " ORDER BY last_name, first_name, person_id;"
            : " WHERE person_status = 'Active' ORDER BY last_name, first_name, person_id;");
        var people = new List<PersonProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            people.Add(ReadPerson(reader));
        }
        return people;
    }

    public async Task AddHouseholdAsync(Household household, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(household);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO households(household_id, name, statement_name, household_status, created_utc, archived_utc)
            VALUES ($household_id, $name, $statement_name, $household_status, $created_utc, $archived_utc);
            """;
        BindHousehold(command, household, includeCreatedUtc: true);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateHouseholdAsync(Household household, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(household);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE households SET
                name = $name,
                statement_name = $statement_name,
                household_status = $household_status,
                archived_utc = $archived_utc
            WHERE household_id = $household_id;
            """;
        BindHousehold(command, household, includeCreatedUtc: false);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The household could not be updated because it no longer exists.");
        }
    }

    public async Task<Household?> GetHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        if (householdId == Guid.Empty)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = HouseholdSelect + " WHERE household_id = $household_id;";
        command.Parameters.AddWithValue("$household_id", householdId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHousehold(reader) : null;
    }

    public async Task<IReadOnlyList<Household>> GetHouseholdsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = HouseholdSelect + (includeArchived
            ? " ORDER BY name, household_id;"
            : " WHERE household_status = 'Active' ORDER BY name, household_id;");
        var households = new List<Household>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            households.Add(ReadHousehold(reader));
        }
        return households;
    }

    public async Task AddGivingCategoryAsync(GivingCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO giving_categories(
                giving_category_id, code, name, description, group_name, category_status, created_utc, archived_utc)
            VALUES (
                $giving_category_id, $code, $name, $description, $group_name, $category_status, $created_utc, $archived_utc);
            """;
        BindGivingCategory(command, category, includeCreatedUtc: true);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateGivingCategoryAsync(GivingCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE giving_categories SET
                code = $code,
                name = $name,
                description = $description,
                group_name = $group_name,
                category_status = $category_status,
                archived_utc = $archived_utc
            WHERE giving_category_id = $giving_category_id;
            """;
        BindGivingCategory(command, category, includeCreatedUtc: false);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The giving category could not be updated because it no longer exists.");
        }
    }

    public async Task<GivingCategory?> GetGivingCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        if (categoryId == Guid.Empty)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = GivingCategorySelect + " WHERE giving_category_id = $giving_category_id;";
        command.Parameters.AddWithValue("$giving_category_id", categoryId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadGivingCategory(reader) : null;
    }

    public async Task<IReadOnlyList<GivingCategory>> GetGivingCategoriesAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = GivingCategorySelect + (includeArchived
            ? " ORDER BY group_name, name, giving_category_id;"
            : " WHERE category_status = 'Active' ORDER BY group_name, name, giving_category_id;");
        var categories = new List<GivingCategory>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(ReadGivingCategory(reader));
        }
        return categories;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static void BindPerson(SqliteCommand command, PersonProfile person, bool includeCreatedUtc)
    {
        command.Parameters.AddWithValue("$person_id", person.Id.ToString("D"));
        command.Parameters.AddWithValue("$first_name", person.FirstName);
        command.Parameters.AddWithValue("$middle_name", person.MiddleName);
        command.Parameters.AddWithValue("$last_name", person.LastName);
        command.Parameters.AddWithValue("$preferred_name", person.PreferredName);
        command.Parameters.AddWithValue("$email", person.Email);
        command.Parameters.AddWithValue("$phone", person.Phone);
        command.Parameters.AddWithValue("$member_number", person.MemberNumber);
        command.Parameters.AddWithValue("$is_member", person.IsMember ? 1 : 0);
        command.Parameters.AddWithValue("$is_donor", person.IsDonor ? 1 : 0);
        command.Parameters.AddWithValue("$household_id", person.HouseholdId.HasValue ? person.HouseholdId.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue("$person_status", person.Status.ToString());
        command.Parameters.AddWithValue("$archived_utc", person.ArchivedUtc.HasValue ? person.ArchivedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        if (includeCreatedUtc)
        {
            command.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    private static void BindHousehold(SqliteCommand command, Household household, bool includeCreatedUtc)
    {
        command.Parameters.AddWithValue("$household_id", household.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", household.Name);
        command.Parameters.AddWithValue("$statement_name", household.StatementName);
        command.Parameters.AddWithValue("$household_status", household.Status.ToString());
        command.Parameters.AddWithValue("$archived_utc", household.ArchivedUtc.HasValue ? household.ArchivedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        if (includeCreatedUtc)
        {
            command.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    private static void BindGivingCategory(SqliteCommand command, GivingCategory category, bool includeCreatedUtc)
    {
        command.Parameters.AddWithValue("$giving_category_id", category.Id.ToString("D"));
        command.Parameters.AddWithValue("$code", category.Code);
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$description", category.Description);
        command.Parameters.AddWithValue("$group_name", category.GroupName);
        command.Parameters.AddWithValue("$category_status", category.Status.ToString());
        command.Parameters.AddWithValue("$archived_utc", category.ArchivedUtc.HasValue ? category.ArchivedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        if (includeCreatedUtc)
        {
            command.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    private static PersonProfile ReadPerson(DbDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(3),
        reader.GetInt32(8) == 1,
        reader.GetInt32(9) == 1,
        reader.GetString(2),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.IsDBNull(10) ? null : Guid.Parse(reader.GetString(10)),
        Enum.Parse<PersonStatus>(reader.GetString(11), ignoreCase: false),
        reader.IsDBNull(12) ? null : DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static Household ReadHousehold(DbDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        Enum.Parse<HouseholdStatus>(reader.GetString(3), ignoreCase: false),
        reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static GivingCategory ReadGivingCategory(DbDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        Enum.Parse<GivingCategoryStatus>(reader.GetString(5), ignoreCase: false),
        reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private const string PersonSelect = """
        SELECT person_id, first_name, middle_name, last_name, preferred_name, email, phone,
               member_number, is_member, is_donor, household_id, person_status, archived_utc
        FROM person_profiles
        """;

    private const string HouseholdSelect = """
        SELECT household_id, name, statement_name, household_status, archived_utc
        FROM households
        """;

    private const string GivingCategorySelect = """
        SELECT giving_category_id, code, name, description, group_name, category_status, archived_utc
        FROM giving_categories
        """;
}
