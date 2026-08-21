using System.IO;
using ChurchBooks.Accounting.Reconciliation;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase8ReconciliationUxTests
{
    [Fact]
    public async Task ReconciliationWorkspace_InitializesSchemaEight()
    {
        await WithDb(async database =>
        {
            var viewModel = new BankReconciliationViewModel();

            await viewModel.InitializeAsync(database);

            Assert.Empty(viewModel.BankAccounts);
            Assert.Equal("PHP 0.00", viewModel.DifferenceDisplay);
        });
    }

    [Fact]
    public void ReconciliationWorkspace_OffersThreeExplicitAmountConventions()
    {
        var viewModel = new BankReconciliationViewModel();

        Assert.Equal(3, viewModel.AmountConventions.Count);
        Assert.Contains(BankStatementAmountConvention.SignedAmount, viewModel.AmountConventions);
        Assert.Contains(BankStatementAmountConvention.DebitIncreasesBalance, viewModel.AmountConventions);
        Assert.Contains(BankStatementAmountConvention.CreditIncreasesBalance, viewModel.AmountConventions);
    }

    [Fact]
    public async Task BankingWorkspace_InitializesNestedReconciliation()
    {
        await WithDb(async database =>
        {
            var viewModel = new BankingWorkspaceViewModel();

            await viewModel.InitializeAsync(database);

            Assert.NotNull(viewModel.Reconciliation);
            Assert.Empty(viewModel.Reconciliation.BankAccounts);
        });
    }

    [Fact]
    public void BankingView_ContainsReconciliationTab()
    {
        var xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "BankingWorkspaceView.xaml"));

        Assert.Contains("Header=\"Reconciliation\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BankReconciliationView", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconciliationView_ExplainsNoAutomaticAdjustment()
    {
        var xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "BankReconciliationView.xaml"));

        Assert.Contains("never creates an automatic balancing adjustment", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReconciliationView_KeepsEssentialActionsVisible()
    {
        var xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "BankReconciliationView.xaml"));

        Assert.Contains("Link Statement Rows", xaml, StringComparison.Ordinal);
        Assert.Contains("Match Selected", xaml, StringComparison.Ordinal);
        Assert.Contains("Complete &amp; Lock", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AliasService_ResolvesReconcileBankToBanking()
    {
        var service = new TerminologyAliasService();

        Assert.True(service.TryResolve("reconcile bank", out var section));
        Assert.Equal(WorkspaceSection.Banking, section);
    }

    [Fact]
    public void SmartImport_NoAutomaticPostingContractRemainsVisible()
    {
        var xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "ImportWorkspaceView.xaml"));

        Assert.Contains("never posts automatically", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovedMainWindow_RemainsMaximizedAndBounded()
    {
        var xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "MainWindow.xaml"));

        Assert.Contains("WindowState=\"Maximized\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"1180\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"700\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MainWindowViewModel_ReportsSchemaEightOrLater()
    {
        await WithDb(async database =>
        {
            var viewModel = new MainWindowViewModel();

            await viewModel.InitializeAsync(database);

            var status = viewModel.DatabaseStatus;
            var marker = "schema v";
            var markerIndex = status.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            Assert.True(markerIndex >= 0);

            var versionStart = markerIndex + marker.Length;
            var versionEnd = versionStart;
            while (versionEnd < status.Length && char.IsDigit(status[versionEnd]))
            {
                versionEnd++;
            }

            Assert.True(versionEnd > versionStart);
            var versionText = status.Substring(versionStart, versionEnd - versionStart);
            Assert.True(int.TryParse(versionText, out var schemaVersion));
            Assert.True(schemaVersion >= 8);
            Assert.Contains("bank reconciliation", status, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase8.AppTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(root, "test.db"));
            await database.InitializeAsync();
            await action(database);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(root);
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string root)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt < 7)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100);
            }
        }
    }
}
