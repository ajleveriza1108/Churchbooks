using System.IO;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.Accounting.People;
using ChurchBooks.Accounting.Periods;
using ChurchBooks.Accounting.Services;
using ChurchBooks.Accounting.Setup;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class Phase6DataTests
{
    [Fact]
    public async Task SchemaSix_CreatesPersonalizationAndBankingTables()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            await using var connection = database.CreateConnection();
            await connection.OpenAsync();

            var requiredTables = new[]
            {
                "organization_profile",
                "terminology_overrides",
                "custom_search_aliases",
                "bank_accounts",
                "giving_category_income_accounts",
                "bank_deposits",
                "bank_deposit_contributions"
            };

            foreach (var table in requiredTables)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name;";
                command.Parameters.AddWithValue("$name", table);
                Assert.NotNull(await command.ExecuteScalarAsync());
            }
        });
    }

    [Fact]
    public async Task SetupStore_RoundTripsOrganization()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSetupStore(database);

            await store.SaveOrganizationProfileAsync(
                new OrganizationProfile(
                    "Grace Church",
                    "Grace Church Inc",
                    CurrencyCode.Php,
                    1,
                    "PH",
                    "123",
                    setupComplete: true));

            var profile = await store.GetOrganizationProfileAsync();
            Assert.NotNull(profile);
            Assert.Equal("Grace Church", profile!.DisplayName);
            Assert.True(profile.SetupComplete);
        });
    }

    [Fact]
    public async Task SetupStore_RoundTripsTerminology()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSetupStore(database);

            await store.SaveTerminologyAsync(
                new TerminologyDefinition(TerminologyKeys.Member, "Partner", "Partners"));

            var terms = await store.GetTerminologyOverridesAsync();
            Assert.Contains(
                terms,
                item => item.Key == TerminologyKeys.Member && item.Plural == "Partners");
        });
    }

    [Fact]
    public async Task SetupStore_RoundTripsCustomAlias()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSetupStore(database);
            var alias = new CustomSearchAlias(Guid.NewGuid(), "Giving", "Seed");

            await store.AddCustomSearchAliasAsync(alias);
            Assert.Single(await store.GetCustomSearchAliasesAsync());

            await store.DeleteCustomSearchAliasAsync(alias.Id);
            Assert.Empty(await store.GetCustomSearchAliasesAsync());
        });
    }

    [Fact]
    public async Task AccountingStore_FindsOpenPeriodAndAccountType()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteAccountingStore(database);
            var account = new Account(Guid.NewGuid(), "4000", "Income", AccountType.Income);
            await store.AddAccountAsync(account);
            await store.AddPeriodAsync(
                new AccountingPeriod(
                    Guid.NewGuid(),
                    "FY",
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31)));

            Assert.Equal(account.Id, (await store.GetAccountByCodeAsync("4000"))!.Id);
            Assert.Single(await store.GetAccountsByTypeAsync(AccountType.Income));
            Assert.NotNull(await store.GetOpenPeriodForDateAsync(new DateOnly(2026, 8, 20)));
        });
    }

    [Fact]
    public async Task BankingStore_AddBankAccountIsAtomicWithLedger()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteBankingStore(database);
            var ledger = new Account(Guid.NewGuid(), "1010", "Checking", AccountType.Asset);
            var bank = new BankAccount(
                Guid.NewGuid(),
                "Operating",
                "Bank",
                "1234",
                CurrencyCode.Php,
                ledger.Id);

            await store.AddBankAccountAsync(bank, ledger);

            Assert.Single(await store.GetBankAccountsAsync());
            Assert.NotNull(await new SqliteAccountingStore(database).GetAccountByCodeAsync("1010"));
        });
    }

    [Fact]
    public async Task BankingStore_RoundTripsCategoryMapping()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var peopleStore = new SqlitePeopleGivingStore(database);
            var category = (await peopleStore.GetGivingCategoriesAsync()).First();
            var account = new Account(Guid.NewGuid(), "4000", "Income", AccountType.Income);
            await new SqliteAccountingStore(database).AddAccountAsync(account);
            var bankingStore = new SqliteBankingStore(database);

            await bankingStore.SetGivingCategoryIncomeMappingAsync(
                new GivingCategoryIncomeMapping(category.Id, account.Id));

            var mappings = await bankingStore.GetGivingCategoryIncomeMappingsAsync();
            Assert.Equal(account.Id, mappings[category.Id]);
        });
    }

    [Fact]
    public async Task Phase6Migration_IsIdempotent()
    {
        await WithDb(async database =>
        {
            var migrator = new Phase6DatabaseMigrator(database);
            await migrator.InitializeAsync();
            await migrator.InitializeAsync();

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key='schema_version';";

            Assert.Equal("6", await command.ExecuteScalarAsync());
        });
    }

    [Fact]
    public async Task BankAccountName_IsUnique()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteBankingStore(database);
            var firstLedger = new Account(Guid.NewGuid(), "1010", "One", AccountType.Asset);
            var secondLedger = new Account(Guid.NewGuid(), "1020", "Two", AccountType.Asset);

            await store.AddBankAccountAsync(
                new BankAccount(
                    Guid.NewGuid(),
                    "Operating",
                    "A",
                    string.Empty,
                    CurrencyCode.Php,
                    firstLedger.Id),
                firstLedger);

            await Assert.ThrowsAsync<SqliteException>(() =>
                store.AddBankAccountAsync(
                    new BankAccount(
                        Guid.NewGuid(),
                        "Operating",
                        "B",
                        string.Empty,
                        CurrencyCode.Php,
                        secondLedger.Id),
                    secondLedger));
        });
    }

    [Fact]
    public async Task SetupProfile_DefaultCanRemainIncomplete()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var store = new SqliteSetupStore(database);

            Assert.Null(await store.GetOrganizationProfileAsync());
        });
    }

    [Fact]
    public async Task FirstRunSetup_CompleteCreatesPeriodIncomeAccountAndCategoryMapping()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var setupStore = new SqliteSetupStore(database);
            var accountingStore = new SqliteAccountingStore(database);
            var peopleStore = new SqlitePeopleGivingStore(database);
            var bankingStore = new SqliteBankingStore(database);
            var service = new FirstRunSetupService(
                setupStore,
                accountingStore,
                peopleStore,
                bankingStore);

            await service.CompleteAsync(
                new OrganizationProfile(
                    "Grace Church",
                    "Grace Church",
                    CurrencyCode.Php,
                    1,
                    "PH"),
                new[]
                {
                    new TerminologyDefinition(TerminologyKeys.Member, "Partner", "Partners")
                });

            var profile = await setupStore.GetOrganizationProfileAsync();
            Assert.NotNull(profile);
            Assert.True(profile!.SetupComplete);
            Assert.NotNull(
                await accountingStore.GetOpenPeriodForDateAsync(
                    DateOnly.FromDateTime(DateTime.Today)));

            var incomeAccounts = await accountingStore.GetAccountsByTypeAsync(AccountType.Income);
            Assert.NotEmpty(incomeAccounts);

            var tithes = (await peopleStore.GetGivingCategoriesAsync())
                .Single(item => item.Code == "TITHE");
            var mappings = await bankingStore.GetGivingCategoryIncomeMappingsAsync();
            Assert.True(mappings.ContainsKey(tithes.Id));
            Assert.Contains(incomeAccounts, account => account.Id == mappings[tithes.Id]);
        });
    }

    [Fact]
    public async Task BankingService_PostDepositCreatesBalancedFundLedgerAndPreventsReuse()
    {
        await WithDb(async database =>
        {
            await new Phase6DatabaseMigrator(database).InitializeAsync();
            var accountingStore = new SqliteAccountingStore(database);
            var peopleStore = new SqlitePeopleGivingStore(database);
            var fundStore = new SqliteFundAccountingStore(database);
            var offeringStore = new SqliteOfferingStore(database);
            var bankingStore = new SqliteBankingStore(database);
            var service = new BankingManagementService(
                accountingStore,
                fundStore,
                offeringStore,
                bankingStore);

            await accountingStore.AddPeriodAsync(
                new AccountingPeriod(
                    Guid.NewGuid(),
                    "FY 2026",
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31)));

            var person = new PersonProfile(Guid.NewGuid(), "Ana", "Santos", true, true);
            await peopleStore.AddPersonAsync(person);
            var category = (await peopleStore.GetGivingCategoriesAsync())
                .Single(item => item.Code == "TITHE");
            var fund = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
            await fundStore.AddFundAsync(fund);
            var batch = new OfferingBatch(
                Guid.NewGuid(),
                new DateOnly(2026, 8, 16),
                "Sunday Service");
            await offeringStore.AddBatchAsync(batch);
            var contribution = new Contribution(
                Guid.NewGuid(),
                batch.Id,
                person.Id,
                batch.ServiceDate,
                CurrencyCode.Php,
                new[]
                {
                    new ContributionLine(Guid.NewGuid(), category.Id, fund.Id, 500m)
                },
                "ENV-1");
            await offeringStore.SaveContributionAsync(contribution);

            var bankLedger = new Account(
                Guid.NewGuid(),
                "1010",
                "Operating Bank",
                AccountType.Asset);
            var bank = new BankAccount(
                Guid.NewGuid(),
                "Operating",
                "Test Bank",
                "1234",
                CurrencyCode.Php,
                bankLedger.Id);
            await service.AddBankAccountAsync(bank, bankLedger);

            var income = new Account(
                Guid.NewGuid(),
                "4000",
                "Contribution Income",
                AccountType.Income);
            await accountingStore.AddAccountAsync(income);
            await bankingStore.SetGivingCategoryIncomeMappingAsync(
                new GivingCategoryIncomeMapping(category.Id, income.Id));

            var deposit = await service.CreateDepositAsync(
                bank.Id,
                new DateOnly(2026, 8, 17),
                new[] { contribution.Id },
                "DEP-1",
                "Sunday deposit");
            var preview = await service.PreviewPostingAsync(deposit.Id);

            Assert.Equal(500m, preview.FundJournal.Entry.TotalDebit);
            Assert.Equal(500m, preview.FundJournal.Entry.TotalCredit);
            Assert.All(
                preview.FundJournal.Entry.Lines,
                line => Assert.Contains(
                    preview.FundJournal.Assignments,
                    assignment => assignment.JournalLineId == line.Id && assignment.FundId == fund.Id));

            await service.PostDepositAsync(deposit.Id);

            var postedDeposit = await bankingStore.GetDepositAsync(deposit.Id);
            Assert.Equal(BankDepositStatus.Posted, postedDeposit!.Status);
            Assert.Contains(
                await accountingStore.GetLedgerAsync(bankLedger.Id),
                line => line.Debit == 500m && line.Credit == 0m);
            Assert.Contains(
                await accountingStore.GetLedgerAsync(income.Id),
                line => line.Credit == 500m && line.Debit == 0m);

            await Assert.ThrowsAsync<BankingManagementException>(() =>
                service.CreateDepositAsync(
                    bank.Id,
                    new DateOnly(2026, 8, 18),
                    new[] { contribution.Id },
                    "DEP-2",
                    "Duplicate"));
        });
    }

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase6.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await action(new ChurchBooksDatabase(Path.Combine(root, "test.db")));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(root);
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string root)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(80);
            }
        }
    }
}
