using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Accounting.Periods;
using ChurchBooks.Accounting.Services;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ChurchBooks.Data.Tests;

public sealed class FundAccountingDataTests
{
    [Fact]
    public async Task Migrator_CreatesFundSchemaAndPhaseMarkers()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(testRoot, "fund.db"));
            var migrator = new FundAccountingDatabaseMigrator(database);
            await migrator.InitializeAsync();
            await migrator.InitializeAsync();

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            var tables = await ReadTableNamesAsync(connection);

            Assert.Contains("schema_migrations", tables);
            Assert.Contains("funds", tables);
            Assert.Contains("journal_line_funds", tables);
            Assert.Equal("3B", await ReadSchemaValueAsync(connection, "phase"));
            Assert.Equal("3", await ReadSchemaValueAsync(connection, "schema_version"));
            Assert.Equal("1", await ReadSchemaValueAsync(connection, "fund_accounting"));
            Assert.Equal(3, await CountSchemaMigrationsAsync(connection));
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task Migrator_ConflictingMigrationHistory_FailsClosedWithoutAdvertisingPhase3B()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(testRoot, "conflict.db"));
            await database.InitializeAsync();

            await using (var connection = database.CreateConnection())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE schema_migrations (
                        migration_key TEXT NOT NULL PRIMARY KEY,
                        schema_version INTEGER NOT NULL UNIQUE,
                        description TEXT NOT NULL,
                        applied_utc TEXT NOT NULL
                    );
                    INSERT INTO schema_migrations(migration_key, schema_version, description, applied_utc)
                    VALUES ('003_fund_accounting', 99, 'Conflicting test history', '2026-08-19T00:00:00Z');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new FundAccountingDatabaseMigrator(database).InitializeAsync());
            Assert.Contains("Schema migration history conflict", error.Message, StringComparison.Ordinal);

            await using var verifyConnection = database.CreateConnection();
            await verifyConnection.OpenAsync();
            Assert.Equal("2", await ReadSchemaValueAsync(verifyConnection, "phase"));
            var tables = await ReadTableNamesAsync(verifyConnection);
            Assert.DoesNotContain("funds", tables);
            Assert.DoesNotContain("journal_line_funds", tables);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task Migrator_UpgradesExistingPhase2DatabaseWithoutChangingPostedLedger()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(testRoot, "upgrade.db"));
            await database.InitializeAsync();
            var accountingStore = new SqliteAccountingStore(database);
            var engine = new AccountingEngine(accountingStore);
            var cash = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
            var income = new Account(Guid.NewGuid(), "4000", "Giving", AccountType.Income);
            var period = new AccountingPeriod(Guid.NewGuid(), "August 2026", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
            await accountingStore.AddAccountAsync(cash);
            await accountingStore.AddAccountAsync(income);
            await accountingStore.AddPeriodAsync(period);
            var journal = new JournalEntry(
                Guid.NewGuid(),
                "PH2-UPGRADE",
                period.Id,
                new DateOnly(2026, 8, 9),
                "Existing Phase 2 journal",
                CurrencyCode.Php,
                new[]
                {
                    new JournalLine(Guid.NewGuid(), cash.Id, 321.45m, 0m),
                    new JournalLine(Guid.NewGuid(), income.Id, 0m, 321.45m)
                });
            await engine.PostJournalAsync(journal);

            await new FundAccountingDatabaseMigrator(database).InitializeAsync();

            var ledger = await accountingStore.GetLedgerAsync(cash.Id);
            Assert.True(await accountingStore.JournalExistsAsync(journal.Id));
            Assert.Single(ledger);
            Assert.Equal(321.45m, ledger[0].Debit);
            Assert.Equal(0m, ledger[0].Credit);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task FundStore_AddArchiveReactivate_RoundTripsExactly()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(testRoot, "fund.db"));
            await new FundAccountingDatabaseMigrator(database).InitializeAsync();
            var store = new SqliteFundAccountingStore(database);
            var fund = new Fund(Guid.NewGuid(), "MISSIONS", "Missions", FundRestriction.DonorRestricted, purpose: "Mission support");

            await store.AddFundAsync(fund);
            var archivedAt = new DateTimeOffset(2026, 8, 19, 18, 45, 0, TimeSpan.FromHours(8));
            await store.UpdateFundAsync(fund.Archive(archivedAt));
            var archived = Assert.IsType<Fund>(await store.GetFundAsync(fund.Id));
            Assert.Equal(FundStatus.Archived, archived.Status);
            Assert.Equal(archivedAt, archived.ArchivedUtc);

            await store.UpdateFundAsync(archived.Reactivate());
            var active = Assert.IsType<Fund>(await store.GetFundAsync(fund.Id));
            Assert.Equal(FundStatus.Active, active.Status);
            Assert.Null(active.ArchivedUtc);
            Assert.Equal("Mission support", active.Purpose);
            Assert.Equal(FundOverspendPolicy.Block, active.OverspendPolicy);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task FundEngine_SplitDonation_PersistsAssignmentsAndExactFundBalances()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(testRoot);
            var general = new Fund(Guid.NewGuid(), "GEN", "General");
            var missions = new Fund(Guid.NewGuid(), "MIS", "Missions", FundRestriction.DonorRestricted);
            await setup.FundStore.AddFundAsync(general);
            await setup.FundStore.AddFundAsync(missions);

            var journal = CreateSplitDonation(setup.Period.Id, setup.Cash, setup.Income, general, missions, 600m, 400m, "JE-FUND-001", new DateOnly(2026, 8, 10));
            var postedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.FromHours(8));
            var posted = await setup.Engine.PostJournalAsync(journal, postedAt);
            var balances = await setup.FundStore.GetFundBalancesAsync(new[] { general.Id, missions.Id });
            var generalActivity = await setup.FundStore.GetFundActivityAsync(general.Id);
            var missionActivity = await setup.FundStore.GetFundActivityAsync(missions.Id);

            Assert.Equal(postedAt, posted.PostedUtc);
            Assert.Empty(posted.Warnings);
            Assert.Equal(600m, balances[general.Id]);
            Assert.Equal(400m, balances[missions.Id]);
            Assert.Equal(2, generalActivity.Count);
            Assert.Equal(2, missionActivity.Count);
            Assert.True(await setup.AccountingStore.JournalExistsAsync(journal.Entry.Id));
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task FundEngine_InternalTransfer_MovesFundBalanceWithoutChangingConsolidatedBankBalance()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(testRoot);
            var general = new Fund(Guid.NewGuid(), "GEN", "General");
            var missions = new Fund(Guid.NewGuid(), "MIS", "Missions", FundRestriction.DonorRestricted);
            var transferEquity = new Account(Guid.NewGuid(), "3100", "Interfund Transfer Clearing", AccountType.Equity);
            await setup.AccountingStore.AddAccountAsync(transferEquity);
            await setup.FundStore.AddFundAsync(general);
            await setup.FundStore.AddFundAsync(missions);

            await setup.Engine.PostJournalAsync(CreateSingleFundDonation(setup.Period.Id, setup.Cash, setup.Income, general, 500m, "JE-FUND-002", new DateOnly(2026, 8, 11)));
            var transfer = FundTransferJournalFactory.Create(
                Guid.NewGuid(),
                "FT-000001",
                setup.Period.Id,
                new DateOnly(2026, 8, 12),
                "Move designation to Missions",
                CurrencyCode.Php,
                general,
                missions,
                setup.Cash,
                setup.Cash,
                transferEquity,
                200m,
                "BOARD-2026-08");
            await setup.Engine.PostJournalAsync(transfer);

            var balances = await setup.FundStore.GetFundBalancesAsync(new[] { general.Id, missions.Id });
            var bankLedger = await setup.AccountingStore.GetLedgerAsync(setup.Cash.Id);
            var bankBalance = bankLedger.Sum(line => line.Debit - line.Credit);

            Assert.Equal(300m, balances[general.Id]);
            Assert.Equal(200m, balances[missions.Id]);
            Assert.Equal(500m, bankBalance);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task FundEngine_BlockOverspend_RejectsBeforeJournalPersistence()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(testRoot);
            var missions = new Fund(Guid.NewGuid(), "MIS", "Missions", FundRestriction.DonorRestricted);
            var expense = new Account(Guid.NewGuid(), "5000", "Mission Expense", AccountType.Expense);
            await setup.AccountingStore.AddAccountAsync(expense);
            await setup.FundStore.AddFundAsync(missions);
            await setup.Engine.PostJournalAsync(CreateSingleFundDonation(setup.Period.Id, setup.Cash, setup.Income, missions, 100m, "JE-FUND-003", new DateOnly(2026, 8, 13)));

            var expenseLines = new[]
            {
                new JournalLine(Guid.NewGuid(), expense.Id, 150m, 0m),
                new JournalLine(Guid.NewGuid(), setup.Cash.Id, 0m, 150m)
            };
            var journal = new JournalEntry(Guid.NewGuid(), "JE-FUND-004", setup.Period.Id, new DateOnly(2026, 8, 14), "Overspend attempt", CurrencyCode.Php, expenseLines);
            var fundJournal = new FundJournalEntry(journal, expenseLines.Select(line => new FundAssignment(line.Id, missions.Id)));

            var error = await Assert.ThrowsAsync<FundPostingException>(() => setup.Engine.PostJournalAsync(fundJournal));
            var balances = await setup.FundStore.GetFundBalancesAsync(new[] { missions.Id });

            Assert.Contains(error.Errors, message => message.Contains("Posting is blocked by the fund overspend policy", StringComparison.Ordinal));
            Assert.False(await setup.AccountingStore.JournalExistsAsync(journal.Id));
            Assert.Equal(100m, balances[missions.Id]);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task FundActivity_DateFilters_ReturnOnlyRequestedPostingRange()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(testRoot);
            var general = new Fund(Guid.NewGuid(), "GEN", "General");
            await setup.FundStore.AddFundAsync(general);
            await setup.Engine.PostJournalAsync(CreateSingleFundDonation(setup.Period.Id, setup.Cash, setup.Income, general, 100m, "JE-FUND-005", new DateOnly(2026, 8, 10)));
            await setup.Engine.PostJournalAsync(CreateSingleFundDonation(setup.Period.Id, setup.Cash, setup.Income, general, 200m, "JE-FUND-006", new DateOnly(2026, 8, 20)));

            var all = await setup.FundStore.GetFundActivityAsync(general.Id);
            var from = await setup.FundStore.GetFundActivityAsync(general.Id, new DateOnly(2026, 8, 15));
            var to = await setup.FundStore.GetFundActivityAsync(general.Id, toDate: new DateOnly(2026, 8, 15));
            var outside = await setup.FundStore.GetFundActivityAsync(general.Id, new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 31));

            Assert.Equal(4, all.Count);
            Assert.Equal(2, from.Count);
            Assert.Equal(2, to.Count);
            Assert.Empty(outside);
            Assert.All(from, line => Assert.Equal(new DateOnly(2026, 8, 20), line.PostingDate));
            Assert.All(to, line => Assert.Equal(new DateOnly(2026, 8, 10), line.PostingDate));
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    private static async Task<TestSetup> CreateSetupAsync(string testRoot)
    {
        var database = new ChurchBooksDatabase(Path.Combine(testRoot, "fund.db"));
        await new FundAccountingDatabaseMigrator(database).InitializeAsync();
        var accountingStore = new SqliteAccountingStore(database);
        var fundStore = new SqliteFundAccountingStore(database);
        var engine = new FundAccountingEngine(accountingStore, fundStore);
        var cash = new Account(Guid.NewGuid(), "1000", "Checking", AccountType.Asset);
        var income = new Account(Guid.NewGuid(), "4000", "Contribution Income", AccountType.Income);
        var period = new AccountingPeriod(Guid.NewGuid(), "August 2026", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        await accountingStore.AddAccountAsync(cash);
        await accountingStore.AddAccountAsync(income);
        await accountingStore.AddPeriodAsync(period);
        return new TestSetup(accountingStore, fundStore, engine, cash, income, period);
    }

    private static FundJournalEntry CreateSingleFundDonation(Guid periodId, Account cash, Account income, Fund fund, decimal amount, string entryNumber, DateOnly date)
    {
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), cash.Id, amount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, amount)
        };
        var journal = new JournalEntry(Guid.NewGuid(), entryNumber, periodId, date, "Contribution", CurrencyCode.Php, lines);
        return new FundJournalEntry(journal, lines.Select(line => new FundAssignment(line.Id, fund.Id)));
    }

    private static FundJournalEntry CreateSplitDonation(Guid periodId, Account cash, Account income, Fund first, Fund second, decimal firstAmount, decimal secondAmount, string entryNumber, DateOnly date)
    {
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), cash.Id, firstAmount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, firstAmount),
            new JournalLine(Guid.NewGuid(), cash.Id, secondAmount, 0m),
            new JournalLine(Guid.NewGuid(), income.Id, 0m, secondAmount)
        };
        var journal = new JournalEntry(Guid.NewGuid(), entryNumber, periodId, date, "Split contribution", CurrencyCode.Php, lines);
        return new FundJournalEntry(journal, new[]
        {
            new FundAssignment(lines[0].Id, first.Id),
            new FundAssignment(lines[1].Id, first.Id),
            new FundAssignment(lines[2].Id, second.Id),
            new FundAssignment(lines[3].Id, second.Id)
        });
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    private static async Task<string?> ReadSchemaValueAsync(SqliteConnection connection, string key)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT schema_value FROM app_schema WHERE schema_key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task<int> CountSchemaMigrationsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_migrations;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string CreateTestRoot() => Path.Combine(Path.GetTempPath(), "ChurchBooks.Fund.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record TestSetup(
        SqliteAccountingStore AccountingStore,
        SqliteFundAccountingStore FundStore,
        FundAccountingEngine Engine,
        Account Cash,
        Account Income,
        AccountingPeriod Period);
    [Fact]
    public async Task GetAllFunds_ExcludesArchivedByDefault()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(testRoot, "fund-list.db"));
            await new FundAccountingDatabaseMigrator(database).InitializeAsync();
            var store = new SqliteFundAccountingStore(database);
            var active = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
            var archived = new Fund(Guid.NewGuid(), "OLD", "Old Project").Archive(DateTimeOffset.UtcNow);
            await store.AddFundAsync(active);
            await store.AddFundAsync(archived);

            var funds = await store.GetAllFundsAsync();

            Assert.Single(funds);
            Assert.Equal(active.Id, funds[0].Id);
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

    [Fact]
    public async Task GetAllFunds_IncludeArchivedReturnsCompleteOrderedFundMaster()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(testRoot, "fund-list-all.db"));
            await new FundAccountingDatabaseMigrator(database).InitializeAsync();
            var store = new SqliteFundAccountingStore(database);
            await store.AddFundAsync(new Fund(Guid.NewGuid(), "MISSIONS", "Missions Fund"));
            await store.AddFundAsync(new Fund(Guid.NewGuid(), "GENERAL", "General Fund").Archive(DateTimeOffset.UtcNow));

            var funds = await store.GetAllFundsAsync(includeArchived: true);

            Assert.Equal(2, funds.Count);
            Assert.Equal(new[] { "GENERAL", "MISSIONS" }, funds.Select(fund => fund.Code).ToArray());
        }
        finally
        {
            DeleteIfPresent(testRoot);
        }
    }

}
