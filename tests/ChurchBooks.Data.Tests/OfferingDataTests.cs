using System.IO;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.Accounting.People;
using ChurchBooks.Accounting.Services;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class OfferingDataTests
{
    [Fact]
    public async Task Migrator_CreatesSchemaVersionFive()
    {
        await WithDatabaseAsync(async database =>
        {
            await new OfferingDatabaseMigrator(database).InitializeAsync();
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key='schema_version';";
            Assert.Equal("5", Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

            command.CommandText = "SELECT COUNT(*) FROM giving_categories WHERE code='TITHE' AND name='Tithes' AND category_status='Active';";
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

            command.CommandText = "SELECT length(giving_category_id) FROM giving_categories WHERE code='TITHE';";
            Assert.Equal(36L, Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        });
    }

    [Fact]
    public async Task Store_RoundTripsOfferingBatch()
    {
        await WithOfferingStoreAsync(async (_, store, _, _) =>
        {
            var batch = new OfferingBatch(Guid.NewGuid(), new DateOnly(2026, 8, 16), "Sunday Morning", "SRV-0816");
            await store.AddBatchAsync(batch);
            var loaded = await store.GetBatchAsync(batch.Id);
            Assert.Equal("Sunday Morning", loaded?.Name);
            Assert.Equal(new DateOnly(2026, 8, 16), loaded?.ServiceDate);
        });
    }

    [Fact]
    public async Task Store_RoundTripsContributionAndBreakdown()
    {
        await WithOfferingStoreAsync(async (database, store, people, funds) =>
        {
            // Simulate the exact Phase 5 R1 legacy starter ID format, then rerun the migrator.
            // The repair must preserve the category while converting its storage key to canonical Guid text.
            await RewriteStarterCategoryAsLegacyCompactIdAsync(database);
            await new OfferingDatabaseMigrator(database).InitializeAsync();
            var setup = await SeedAsync(store, people, funds, donor: true);
            var contribution = new Contribution(Guid.NewGuid(), setup.Batch.Id, setup.Person.Id, setup.Batch.ServiceDate, CurrencyCode.Php,
                new[] { new ContributionLine(Guid.NewGuid(), setup.Category.Id, setup.Fund.Id, 123.45m) }, "ENV-10", "Sunday tithe");
            await store.SaveContributionAsync(contribution);
            var loaded = Assert.Single(await store.GetContributionsAsync(setup.Person.Id));
            Assert.Equal(123.45m, loaded.TotalAmount);
            Assert.Equal("ENV-10", loaded.Reference);
            Assert.Single(loaded.Lines);
        });
    }

    [Fact]
    public async Task ManagementService_RecordsValidIndividualOffering()
    {
        await WithOfferingStoreAsync(async (_, store, people, funds) =>
        {
            var setup = await SeedAsync(store, people, funds, donor: true);
            var service = new OfferingManagementService(store, people, funds);
            var contribution = new Contribution(Guid.NewGuid(), setup.Batch.Id, setup.Person.Id, setup.Batch.ServiceDate, CurrencyCode.Php,
                new[] { new ContributionLine(Guid.NewGuid(), setup.Category.Id, setup.Fund.Id, 500m) });
            await service.RecordContributionAsync(contribution);
            Assert.True(await store.ContributionExistsAsync(contribution.Id));
        });
    }

    [Fact]
    public async Task ManagementService_RejectsPersonNotMarkedDonor()
    {
        await WithOfferingStoreAsync(async (_, store, people, funds) =>
        {
            var setup = await SeedAsync(store, people, funds, donor: false);
            var service = new OfferingManagementService(store, people, funds);
            var contribution = new Contribution(Guid.NewGuid(), setup.Batch.Id, setup.Person.Id, setup.Batch.ServiceDate, CurrencyCode.Php,
                new[] { new ContributionLine(Guid.NewGuid(), setup.Category.Id, setup.Fund.Id, 50m) });
            var ex = await Assert.ThrowsAsync<OfferingManagementException>(() => service.RecordContributionAsync(contribution));
            Assert.Contains("donor", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task<(OfferingBatch Batch, PersonProfile Person, GivingCategory Category, Fund Fund)> SeedAsync(
        SqliteOfferingStore offeringStore, SqlitePeopleGivingStore people, SqliteFundAccountingStore funds, bool donor)
    {
        var person = new PersonProfile(Guid.NewGuid(), "Ana", "Santos", isMember: true, isDonor: donor);
        var category = (await people.GetGivingCategoriesAsync(includeArchived: true))
            .Single(item => item.Code.Equals("TITHE", StringComparison.OrdinalIgnoreCase));
        var fund = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
        var batch = new OfferingBatch(Guid.NewGuid(), new DateOnly(2026, 8, 16), "Sunday Service");
        await people.AddPersonAsync(person);
        await funds.AddFundAsync(fund);
        await offeringStore.AddBatchAsync(batch);
        return (batch, person, category, fund);
    }

    private static async Task RewriteStarterCategoryAsLegacyCompactIdAsync(ChurchBooksDatabase database)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE giving_categories SET giving_category_id = replace(giving_category_id, '-', '') WHERE code='TITHE';";
        Assert.Equal(1, await command.ExecuteNonQueryAsync());

        command.CommandText = "SELECT length(giving_category_id) FROM giving_categories WHERE code='TITHE';";
        Assert.Equal(32L, Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task WithOfferingStoreAsync(Func<ChurchBooksDatabase, SqliteOfferingStore, SqlitePeopleGivingStore, SqliteFundAccountingStore, Task> action)
    {
        await WithDatabaseAsync(async database =>
        {
            await new OfferingDatabaseMigrator(database).InitializeAsync();
            await action(database, new SqliteOfferingStore(database), new SqlitePeopleGivingStore(database), new SqliteFundAccountingStore(database));
        });
    }

    private static async Task WithDatabaseAsync(Func<ChurchBooksDatabase, Task> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase5.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { await action(new ChurchBooksDatabase(Path.Combine(directory, "phase5.db"))); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
