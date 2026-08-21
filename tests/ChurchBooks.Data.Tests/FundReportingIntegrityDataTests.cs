using System.IO;
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

public sealed class FundReportingIntegrityDataTests
{
    [Fact]
    public async Task GetFundBalancesAsOf_ExcludesFuturePostedActivity()
    {
        var root = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(root);
            var general = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
            await setup.FundStore.AddFundAsync(general);
            await setup.Engine.PostJournalAsync(CreateDonation(setup, general, 100m, "RPT-001", new DateOnly(2026, 8, 10)));
            await setup.Engine.PostJournalAsync(CreateDonation(setup, general, 250m, "RPT-002", new DateOnly(2026, 9, 10)));

            var august = await setup.FundStore.GetFundBalancesAsOfAsync(new[] { general.Id }, new DateOnly(2026, 8, 31));
            var september = await setup.FundStore.GetFundBalancesAsOfAsync(new[] { general.Id }, new DateOnly(2026, 9, 30));

            Assert.Equal(100m, august[general.Id]);
            Assert.Equal(350m, september[general.Id]);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task IntegrityScanner_CleanFundAwareJournalHasNoFindings()
    {
        var root = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(root);
            var general = new Fund(Guid.NewGuid(), "GENERAL", "General Fund", purpose: "General operations");
            await setup.FundStore.AddFundAsync(general);
            await setup.Engine.PostJournalAsync(CreateDonation(setup, general, 100m, "INT-001", new DateOnly(2026, 8, 10)));

            var report = await new SqliteFundIntegrityScanner(setup.Database, setup.FundStore).ScanAsync();

            Assert.True(report.IsClean);
            Assert.Empty(report.Findings);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task IntegrityScanner_DetectsMissingAssignmentWithinFundAwareJournal()
    {
        var root = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(root);
            var general = new Fund(Guid.NewGuid(), "GENERAL", "General Fund", purpose: "General operations");
            await setup.FundStore.AddFundAsync(general);
            var entry = CreateDonation(setup, general, 100m, "INT-002", new DateOnly(2026, 8, 10));
            await setup.Engine.PostJournalAsync(entry);

            await using (var connection = setup.Database.CreateConnection())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM journal_line_funds WHERE journal_line_id = $line_id;";
                command.Parameters.AddWithValue("$line_id", entry.Entry.Lines[0].Id.ToString("D"));
                Assert.Equal(1, await command.ExecuteNonQueryAsync());
            }

            var report = await new SqliteFundIntegrityScanner(setup.Database, setup.FundStore).ScanAsync();

            Assert.Contains(report.Findings, finding => finding.Code == "FUND_ASSIGNMENT_GAP");
            Assert.True(report.HasBlockingIssues);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task IntegrityScanner_DetectsPerFundJournalImbalance()
    {
        var root = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(root);
            var general = new Fund(Guid.NewGuid(), "GENERAL", "General Fund", purpose: "General operations");
            var missions = new Fund(Guid.NewGuid(), "MISSIONS", "Missions Fund", FundRestriction.DonorRestricted, purpose: "Mission support");
            await setup.FundStore.AddFundAsync(general);
            await setup.FundStore.AddFundAsync(missions);
            var entry = CreateDonation(setup, general, 100m, "INT-003", new DateOnly(2026, 8, 10));
            await setup.Engine.PostJournalAsync(entry);

            await using (var connection = setup.Database.CreateConnection())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE journal_line_funds SET fund_id = $fund_id WHERE journal_line_id = $line_id;";
                command.Parameters.AddWithValue("$fund_id", missions.Id.ToString("D"));
                command.Parameters.AddWithValue("$line_id", entry.Entry.Lines[0].Id.ToString("D"));
                Assert.Equal(1, await command.ExecuteNonQueryAsync());
            }

            var report = await new SqliteFundIntegrityScanner(setup.Database, setup.FundStore).ScanAsync();

            Assert.Equal(2, report.Findings.Count(finding => finding.Code == "FUND_JOURNAL_IMBALANCE"));
            Assert.True(report.HasBlockingIssues);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task IntegrityScanner_WarnsWhenArchivedFundRetainsBalance()
    {
        var root = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(root);
            var fund = new Fund(Guid.NewGuid(), "PROJECT", "Project Fund", purpose: "Capital project");
            await setup.FundStore.AddFundAsync(fund);
            await setup.Engine.PostJournalAsync(CreateDonation(setup, fund, 100m, "INT-004", new DateOnly(2026, 8, 10)));
            await setup.FundStore.UpdateFundAsync(fund.Archive(DateTimeOffset.UtcNow));

            var report = await new SqliteFundIntegrityScanner(setup.Database, setup.FundStore).ScanAsync();

            Assert.Contains(report.Findings, finding => finding.Code == "ARCHIVED_NONZERO_BALANCE");
            Assert.Equal(0, report.CriticalCount);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public async Task IntegrityScanner_WarnsWhenRestrictedPurposeIsMissing()
    {
        var root = CreateTestRoot();
        try
        {
            var setup = await CreateSetupAsync(root);
            var restricted = new Fund(Guid.NewGuid(), "RESTRICTED", "Restricted Fund", FundRestriction.DonorRestricted);
            await setup.FundStore.AddFundAsync(restricted);

            var report = await new SqliteFundIntegrityScanner(setup.Database, setup.FundStore).ScanAsync();

            Assert.Contains(report.Findings, finding => finding.Code == "RESTRICTED_PURPOSE_MISSING");
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    private static async Task<TestSetup> CreateSetupAsync(string root)
    {
        var database = new ChurchBooksDatabase(Path.Combine(root, "phase3d.db"));
        await new FundAccountingDatabaseMigrator(database).InitializeAsync();
        var accountingStore = new SqliteAccountingStore(database);
        var fundStore = new SqliteFundAccountingStore(database);
        var engine = new FundAccountingEngine(accountingStore, fundStore);
        var cash = new Account(Guid.NewGuid(), "1000", "Checking", AccountType.Asset);
        var income = new Account(Guid.NewGuid(), "4000", "Contribution Income", AccountType.Income);
        var period = new AccountingPeriod(Guid.NewGuid(), "2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        await accountingStore.AddAccountAsync(cash);
        await accountingStore.AddAccountAsync(income);
        await accountingStore.AddPeriodAsync(period);
        return new TestSetup(database, fundStore, engine, cash, income, period);
    }

    private static FundJournalEntry CreateDonation(TestSetup setup, Fund fund, decimal amount, string entryNumber, DateOnly date)
    {
        var lines = new[]
        {
            new JournalLine(Guid.NewGuid(), setup.Cash.Id, amount, 0m),
            new JournalLine(Guid.NewGuid(), setup.Income.Id, 0m, amount)
        };
        var journal = new JournalEntry(Guid.NewGuid(), entryNumber, setup.Period.Id, date, "Contribution", CurrencyCode.Php, lines);
        return new FundJournalEntry(journal, lines.Select(line => new FundAssignment(line.Id, fund.Id)));
    }

    private static string CreateTestRoot() => Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase3D.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record TestSetup(
        ChurchBooksDatabase Database,
        SqliteFundAccountingStore FundStore,
        FundAccountingEngine Engine,
        Account Cash,
        Account Income,
        AccountingPeriod Period);
}
