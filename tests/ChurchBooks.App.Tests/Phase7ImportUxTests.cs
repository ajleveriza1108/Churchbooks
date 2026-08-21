using System.IO;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase7ImportUxTests
{
    [Fact]
    public async Task ImportWorkspace_InitializesSchemaSeven()
    {
        await WithDb(async database =>
        {
            var viewModel = new ImportWorkspaceViewModel();

            await viewModel.InitializeAsync(database);

            Assert.False(viewModel.HasAnalysis);
        });
    }

    [Fact]
    public async Task ImportWorkspace_LoadsCsvAndBuildsMappings()
    {
        await WithDb(async database =>
        {
            await WithFile("Date,Amount\n2026-08-20,10\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);

                await viewModel.LoadFileAsync(path);

                Assert.True(viewModel.HasAnalysis);
                Assert.Equal(2, viewModel.Mappings.Count);
            });
        });
    }

    [Fact]
    public async Task ImportWorkspace_CanSaveTemplate()
    {
        await WithDb(async database =>
        {
            await WithFile("Date,Amount\n2026-08-20,10\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);
                await viewModel.LoadFileAsync(path);

                await viewModel.SaveMappingTemplateCommand.ExecuteAsync(null);

                Assert.Contains(
                    "saved",
                    viewModel.StatusMessage,
                    StringComparison.OrdinalIgnoreCase);
            });
        });
    }

    [Fact]
    public async Task ImportWorkspace_StagesWithoutPosting()
    {
        await WithDb(async database =>
        {
            await WithFile("Date,Amount\n2026-08-20,10\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);
                await viewModel.LoadFileAsync(path);

                await viewModel.ApproveAndStageCommand.ExecuteAsync(null);

                Assert.Contains(
                    "No journal",
                    viewModel.StatusMessage,
                    StringComparison.OrdinalIgnoreCase);
            });
        });
    }

    [Fact]
    public void WorkspaceSection_IncludesImport() =>
        Assert.True(Enum.IsDefined(WorkspaceSection.Import));

    [Fact]
    public void AliasService_ResolvesImport()
    {
        var service = new TerminologyAliasService();

        Assert.True(service.TryResolve("open smart import", out var section));
        Assert.Equal(WorkspaceSection.Import, section);
    }

    [Fact]
    public void MainWindow_HasSmartImportNavigation()
    {
        var xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "MainWindow.xaml"));

        Assert.Contains("ShowImportCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ImportWorkspaceView", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportView_UsesApprovedCompactGrid()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                ProjectRoot(),
                "src",
                "ChurchBooks.App",
                "Views",
                "ImportWorkspaceView.xaml"));

        Assert.Contains("Grid.ColumnDefinitions", xaml, StringComparison.Ordinal);
        Assert.Contains("Approve + Stage", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportView_ExplainsNoAutomaticPosting()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                ProjectRoot(),
                "src",
                "ChurchBooks.App",
                "Views",
                "ImportWorkspaceView.xaml"));

        Assert.Contains("never posts automatically", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovedMainWindow_RemainsMaximized()
    {
        var xaml = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "MainWindow.xaml"));

        Assert.Contains("WindowState=\"Maximized\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"1180\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"700\"", xaml, StringComparison.Ordinal);
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase7.AppTests",
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

    private static async Task WithFile(string content, Func<string, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ChurchBooks.Phase7.AppFiles",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var path = Path.Combine(root, "test.csv");
            await File.WriteAllTextAsync(path, content);
            await action(path);
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
