using System.IO;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10ExpenseUxTests
{
    [Fact]
    public async Task ExpensesWorkspace_InitializesSchemaTen()
    {
        await WithDb(async database =>
        {
            var viewModel = new ExpensesWorkspaceViewModel();
            await viewModel.InitializeAsync(database);
            Assert.Empty(viewModel.Vendors);
            Assert.Empty(viewModel.Expenses);
        });
    }

    [Fact]
    public async Task ExpensesWorkspace_AddVendorIsExplicitMasterDataOnly()
    {
        await WithDb(async database =>
        {
            var viewModel = new ExpensesWorkspaceViewModel();
            await viewModel.InitializeAsync(database);
            viewModel.VendorCode = "V001";
            viewModel.VendorName = "Local Vendor";
            await viewModel.AddVendorCommand.ExecuteAsync(null);
            Assert.Single(viewModel.Vendors);
            Assert.Contains("never post", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ExpensesView_ExplainsExplicitPostingBoundary()
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "ExpensesWorkspaceView.xaml"));
        var viewModelSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "ViewModels", "ExpensesWorkspaceViewModel.cs"));
        Assert.Contains("Nothing posts until", viewModelSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Multiple currencies", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelectedBankAccount?.Currency.Value ?? BaseCurrency", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Guid? vendorId = SelectedVendor is { Status: VendorStatus.Active } activeVendor", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var vendorId = SelectedVendor?.Status == VendorStatus.Active ? SelectedVendor.Id : null;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Smart Import never posts expenses automatically", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bills/payables are intentionally reserved for the next phase", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_HasSingleActiveExpensesNavigationButton()
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "MainWindow.xaml"));
        Assert.Contains("ShowExpensesCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Expenses\" IsEnabled=\"False\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MainWindowViewModel_ReportsSchemaTenOrLater()
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
            while (versionEnd < status.Length && char.IsDigit(status[versionEnd])) versionEnd++;
            Assert.True(int.TryParse(status.Substring(versionStart, versionEnd - versionStart), out var schemaVersion));
            Assert.True(schemaVersion >= 10);
            Assert.Contains("vendors/direct expenses", status, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void GlobalSearch_ResolvesExpenseAndVendorTerms()
    {
        var service = new TerminologyAliasService();
        Assert.True(service.TryResolve("expense entry", out var expenseSection));
        Assert.Equal(WorkspaceSection.Expenses, expenseSection);
        Assert.True(service.TryResolve("vendor list", out var vendorSection));
        Assert.Equal(WorkspaceSection.Expenses, vendorSection);
    }

    [Fact]
    public void WorkspaceSection_ContainsExpensesWithoutRenumberingPriorSections()
    {
        Assert.Equal(9, (int)WorkspaceSection.Import);
        Assert.Equal(10, (int)WorkspaceSection.Expenses);
    }

    [Fact]
    public void AdaptiveImport_NoAutomaticPostingContractRemainsVisible()
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "ImportWorkspaceView.xaml"));
        Assert.Contains("never posts automatically", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never merges people automatically", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase10.AppTests", Guid.NewGuid().ToString("N"));
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
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
