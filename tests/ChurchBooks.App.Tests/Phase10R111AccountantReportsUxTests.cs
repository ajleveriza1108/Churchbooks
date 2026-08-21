using System.IO;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Reporting;
using ChurchBooks.App.Services;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R111AccountantReportsUxTests
{
    [Fact]
    public void ReportsCenter_ExposesTrialBalanceIncomeExpenseAndGeneralLedger()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Header=\"Accountant Reports\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Trial Balance\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Income &amp; Expense\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"General Ledger\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Trial Balance, and full General Ledger presentation remain a later reporting phase", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FinancialReports_UsePostedJournalReportingServiceAndNoAutomaticCorrection()
    {
        var source = ReadSource("src", "ChurchBooks.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("new AccountantReportingService(_accountingStore)", source, StringComparison.Ordinal);
        Assert.Contains("BuildTrialBalanceAsync", source, StringComparison.Ordinal);
        Assert.Contains("BuildIncomeExpenseAsync", source, StringComparison.Ordinal);
        Assert.Contains("BuildGeneralLedgerAsync", source, StringComparison.Ordinal);
        Assert.Contains("No automatic correction was made", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsCenter_ProvidesCsvExportCommandsForAllThreeAccountantReports()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("ExportTrialBalanceCsvCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ExportIncomeExpenseCsvCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ExportGeneralLedgerCsvCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvExporter_TrialBalanceWritesDebitCreditTotalsAndDifference()
    {
        var root = CreateTestRoot();
        try
        {
            var report = new TrialBalanceReport(new DateOnly(2026, 8, 31), new[]
            {
                new TrialBalanceRow(Guid.NewGuid(), "1000", "Cash", AccountType.Asset, 100m, 0m),
                new TrialBalanceRow(Guid.NewGuid(), "4000", "Giving Income", AccountType.Income, 0m, 100m)
            });

            var path = new CsvReportExportService().ExportTrialBalanceReport(report, root);
            var text = File.ReadAllText(path);

            Assert.Contains("Totals", text, StringComparison.Ordinal);
            Assert.Contains("Difference", text, StringComparison.Ordinal);
            Assert.Contains("100.00", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public void CsvExporter_IncomeExpenseWritesTotalsAndNetIncome()
    {
        var root = CreateTestRoot();
        try
        {
            var report = new IncomeExpenseReport(
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                new[] { new IncomeExpenseRow(Guid.NewGuid(), "4000", "Giving Income", AccountType.Income, 500m) },
                new[] { new IncomeExpenseRow(Guid.NewGuid(), "6000", "Utilities", AccountType.Expense, 125m) });

            var path = new CsvReportExportService().ExportIncomeExpenseReport(report, root);
            var text = File.ReadAllText(path);

            Assert.Contains("Total Income", text, StringComparison.Ordinal);
            Assert.Contains("Total Expenses", text, StringComparison.Ordinal);
            Assert.Contains("Net Income", text, StringComparison.Ordinal);
            Assert.Contains("375.00", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public void CsvExporter_GeneralLedgerWritesOpeningRunningAndClosingBalances()
    {
        var root = CreateTestRoot();
        try
        {
            var account = new Account(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
            var report = new GeneralLedgerReport(
                account,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                100m,
                new[]
                {
                    new GeneralLedgerReportRow(Guid.NewGuid(), "JE-001", new DateOnly(2026, 8, 5), 75m, 0m, 175m, "Deposit", "REF", "Memo")
                });

            var path = new CsvReportExportService().ExportGeneralLedgerReport(report, root);
            var text = File.ReadAllText(path);

            Assert.Contains("Opening Balance", text, StringComparison.Ordinal);
            Assert.Contains("Running Balance", text, StringComparison.Ordinal);
            Assert.Contains("Closing Balance", text, StringComparison.Ordinal);
            Assert.Contains("175.00", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public void Manifest_LocksAccountantReportsCoreAndDefersBalanceSheet()
    {
        var manifest = ReadSource("churchbooks.manifest.json");
        Assert.Contains("\"trialBalance\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"incomeExpenseStatement\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"generalLedger\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"balanceSheetDeferred\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"automaticCorrection\": false", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void R111_PreservesOneScreenDashboardBackupSafetyAndDeferredLicensing()
    {
        var manifest = ReadSource("churchbooks.manifest.json");
        Assert.Contains("\"dashboardScrollViewerRemoved\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"sqliteOnlineBackup\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"restoreMergesDatabases\": false", manifest, StringComparison.Ordinal);
        Assert.Contains("\"licensingChanged\": false", manifest, StringComparison.Ordinal);
        Assert.Contains("\"accountingBehaviorChanged\": false", manifest, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string CreateTestRoot() => Path.Combine(Path.GetTempPath(), "ChurchBooks.App.R111.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfPresent(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
