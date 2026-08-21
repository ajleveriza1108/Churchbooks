using System.IO;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.People;
using ChurchBooks.Data.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class PeopleGivingDataTests
{
    [Fact]
    public async Task Migrator_CreatesSchemaVersionFour()
    {
        await WithDatabaseAsync(async database =>
        {
            await new PeopleGivingDatabaseMigrator(database).InitializeAsync();
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key = 'schema_version';";

            Assert.Equal("4", Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        });
    }

    [Fact]
    public async Task Store_RoundTripsHousehold()
    {
        await WithStoreAsync(async (_, store) =>
        {
            var household = new Household(Guid.NewGuid(), "Santos Family", "The Santos Family");
            await store.AddHouseholdAsync(household);

            var loaded = await store.GetHouseholdAsync(household.Id);

            Assert.Equal("The Santos Family", loaded?.StatementName);
        });
    }

    [Fact]
    public async Task Store_RoundTripsPersonWithHousehold()
    {
        await WithStoreAsync(async (_, store) =>
        {
            var household = new Household(Guid.NewGuid(), "Reyes Family");
            await store.AddHouseholdAsync(household);
            var person = new PersonProfile(Guid.NewGuid(), "Ben", "Reyes", true, true, preferredName: "Benny", email: "ben@example.com", memberNumber: "M-200", householdId: household.Id);
            await store.AddPersonAsync(person);

            var loaded = await store.GetPersonAsync(person.Id);

            Assert.Equal(household.Id, loaded?.HouseholdId);
            Assert.Equal("Benny Reyes", loaded?.DisplayName);
        });
    }

    [Fact]
    public async Task Store_ExcludesArchivedPeopleByDefault()
    {
        await WithStoreAsync(async (_, store) =>
        {
            var active = new PersonProfile(Guid.NewGuid(), "Ana", "Santos", true, true);
            var archived = new PersonProfile(Guid.NewGuid(), "Old", "Record", false, true).Archive(DateTimeOffset.UtcNow);
            await store.AddPersonAsync(active);
            await store.AddPersonAsync(archived);

            var current = await store.GetPeopleAsync();
            var all = await store.GetPeopleAsync(includeArchived: true);

            Assert.Single(current);
            Assert.Equal(2, all.Count);
        });
    }

    [Fact]
    public async Task Store_RoundTripsGivingCategory()
    {
        await WithStoreAsync(async (_, store) =>
        {
            var category = new GivingCategory(Guid.NewGuid(), "TITHE", "Tithes", "Regular tithe", "Core Giving");
            await store.AddGivingCategoryAsync(category);

            var loaded = await store.GetGivingCategoryAsync(category.Id);

            Assert.Equal("Core Giving", loaded?.GroupName);
            Assert.Equal("Regular tithe", loaded?.Description);
        });
    }

    [Fact]
    public async Task Store_UpdatesPersonWithoutChangingIdentity()
    {
        await WithStoreAsync(async (_, store) =>
        {
            var person = new PersonProfile(Guid.NewGuid(), "Ana", "Santos", true, true);
            await store.AddPersonAsync(person);
            var updated = new PersonProfile(person.Id, "Ana", "Dela Cruz", true, true, email: "ana@example.com");
            await store.UpdatePersonAsync(updated);

            var loaded = await store.GetPersonAsync(person.Id);

            Assert.Equal(person.Id, loaded?.Id);
            Assert.Equal("Dela Cruz", loaded?.LastName);
        });
    }

    [Fact]
    public async Task Database_RejectsDuplicateMemberNumberIgnoringCase()
    {
        await WithStoreAsync(async (_, store) =>
        {
            await store.AddPersonAsync(new PersonProfile(Guid.NewGuid(), "Ana", "Santos", true, true, memberNumber: "M-500"));

            await Assert.ThrowsAsync<SqliteException>(() =>
                store.AddPersonAsync(new PersonProfile(Guid.NewGuid(), "Ben", "Reyes", true, false, memberNumber: "m-500")));
        });
    }

    [Fact]
    public async Task Database_RejectsOrphanHouseholdReference()
    {
        await WithStoreAsync(async (_, store) =>
        {
            await Assert.ThrowsAsync<SqliteException>(() =>
                store.AddPersonAsync(new PersonProfile(Guid.NewGuid(), "Cara", "Lopez", false, true, householdId: Guid.NewGuid())));
        });
    }

    private static async Task WithStoreAsync(Func<ChurchBooksDatabase, SqlitePeopleGivingStore, Task> action)
    {
        await WithDatabaseAsync(async database =>
        {
            await new PeopleGivingDatabaseMigrator(database).InitializeAsync();
            await action(database, new SqlitePeopleGivingStore(database));
        });
    }

    private static async Task WithDatabaseAsync(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase4.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await action(new ChurchBooksDatabase(Path.Combine(root, "phase4.db")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
