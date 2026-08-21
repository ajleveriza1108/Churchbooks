using System.IO;
using ChurchBooks.Accounting.Setup;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase6UxTests
{
    [Fact]
    public async Task SetupWorkspace_FirstDatabaseStartsIncomplete()
    {
        await WithDb(async database =>
        {
            var viewModel = new SetupWorkspaceViewModel();

            await viewModel.InitializeAsync(database);

            Assert.False(viewModel.IsSetupComplete);
            Assert.Equal("My Church", viewModel.OrganizationDisplayName);
        });
    }

    [Fact]
    public async Task SetupWorkspace_CanPersonalizeMemberTerm()
    {
        await WithDb(async database =>
        {
            var viewModel = new SetupWorkspaceViewModel();
            await viewModel.InitializeAsync(database);
            var term = viewModel.Terms.Single(item => item.Key == TerminologyKeys.Member);
            term.Singular = "Partner";
            term.Plural = "Partners";

            await viewModel.SaveSetupCommand.ExecuteAsync(null);

            var reloaded = new SetupWorkspaceViewModel();
            await reloaded.InitializeAsync(database);
            Assert.Equal("Partners", reloaded.MemberPlural);
        });
    }

    [Fact]
    public async Task SetupWorkspace_CanAddCustomAlias()
    {
        await WithDb(async database =>
        {
            var viewModel = new SetupWorkspaceViewModel();
            await viewModel.InitializeAsync(database);
            viewModel.AliasArea = "Giving";
            viewModel.AliasText = "Seed";

            await viewModel.AddAliasCommand.ExecuteAsync(null);

            Assert.Contains(viewModel.CustomAliases, item => item.AliasText == "Seed");
        });
    }

    [Fact]
    public async Task MainWindowViewModel_FirstRunRoutesToSetup()
    {
        await WithDb(async database =>
        {
            var viewModel = new MainWindowViewModel();

            await viewModel.InitializeAsync(database);

            Assert.Equal(WorkspaceSection.Setup, viewModel.CurrentSection);
        });
    }

    [Fact]
    public async Task BankingWorkspace_InitializesSchemaSix()
    {
        await WithDb(async database =>
        {
            var viewModel = new BankingWorkspaceViewModel();

            await viewModel.InitializeAsync(database);

            Assert.Equal(0, viewModel.ActiveBankCount);
        });
    }

    [Fact]
    public void TerminologyAliasService_ResolvesCustomBankingWord()
    {
        var service = new TerminologyAliasService();
        service.SetCustomAliases(
            new[] { new CustomSearchAlias(Guid.NewGuid(), "Banking", "Treasury") });

        Assert.True(service.TryResolve("open treasury", out var section));
        Assert.Equal(WorkspaceSection.Banking, section);
    }

    [Fact]
    public void GivingWorkspace_AppliesPersonalizedTerms()
    {
        var viewModel = new GivingWorkspaceViewModel();
        viewModel.ApplyTerminology(
            new TerminologyCatalog(
                new[] { new TerminologyDefinition(TerminologyKeys.Offering, "Contribution", "Contributions") }));

        Assert.Equal("Contributions", viewModel.OfferingPluralLabel);
    }

    [Fact]
    public void WorkspaceSection_IncludesBankingAndSetup()
    {
        Assert.True(Enum.IsDefined(WorkspaceSection.Banking));
        Assert.True(Enum.IsDefined(WorkspaceSection.Setup));
    }

    [Fact]
    public void ApprovedWindow_RemainsMaximizedNoClippingContract()
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "MainWindow.xaml"));

        Assert.Contains("WindowState=\"Maximized\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"1180\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"700\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_BindsNavigationToPersonalizedTerms()
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "MainWindow.xaml"));

        Assert.Contains("SetupWorkspace.MemberPlural", xaml, StringComparison.Ordinal);
        Assert.Contains("SetupWorkspace.FundPlural", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowBankingCommand", xaml, StringComparison.Ordinal);
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase6.AppTests",
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
