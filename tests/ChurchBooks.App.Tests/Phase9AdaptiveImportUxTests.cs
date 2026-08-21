using System.IO;
using ChurchBooks.Accounting.Importing;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase9AdaptiveImportUxTests
{
    [Fact]
    public async Task ImportWorkspace_InitializesSchemaNine()
    {
        await WithDb(async database =>
        {
            var viewModel = new ImportWorkspaceViewModel();
            await viewModel.InitializeAsync(database);
            Assert.False(viewModel.HasAnalysis);
            Assert.Equal(4, viewModel.StandardTemplates.Count);
        });
    }

    [Fact]
    public async Task ImportWorkspace_DetectsPeopleDirectory()
    {
        await WithDb(async database =>
        {
            await WithFile("Given Name,Surname,Email\nAna,Cruz,ana@example.test\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);
                await viewModel.LoadFileAsync(path);
                Assert.Equal("People Directory", viewModel.DetectedPurpose);
                Assert.True(viewModel.IsPeopleDirectory);
            });
        });
    }

    [Fact]
    public async Task ImportWorkspace_DetectsHeaderBelowTitle()
    {
        await WithDb(async database =>
        {
            await WithFile("Member Directory\nGenerated today\nGiven Name,Surname\nAna,Cruz\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);
                await viewModel.LoadFileAsync(path);
                Assert.Equal("3", viewModel.HeaderRowDisplay);
            });
        });
    }

    [Fact]
    public async Task ImportWorkspace_SavesReusableSourceProfile()
    {
        await WithDb(async database =>
        {
            await WithFile("Given Name,Surname\nAna,Cruz\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);
                await viewModel.LoadFileAsync(path);
                viewModel.SelectedPersonDefaultRole = PersonImportDefaultRole.Member;
                await viewModel.SaveMappingTemplateCommand.ExecuteAsync(null);
                Assert.Contains("saved", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
                Assert.NotNull(viewModel.SelectedSourceProfile);
            });
        });
    }

    [Fact]
    public async Task ImportWorkspace_RecognizesSavedProfileOnNextFile()
    {
        await WithDb(async database =>
        {
            await WithFile("Given Name,Surname\nAna,Cruz\n", async path =>
            {
                var first = new ImportWorkspaceViewModel();
                await first.InitializeAsync(database);
                await first.LoadFileAsync(path);
                first.TemplateName = "Sunday Directory";
                first.SelectedPersonDefaultRole = PersonImportDefaultRole.Member;
                await first.SaveMappingTemplateCommand.ExecuteAsync(null);
                var second = new ImportWorkspaceViewModel();
                await second.InitializeAsync(database);
                await second.LoadFileAsync(path);
                Assert.NotNull(second.SelectedSourceProfile);
                Assert.Contains("Sunday Directory", second.SourceProfileStatus, StringComparison.Ordinal);
            });
        });
    }

    [Fact]
    public async Task PeopleResolution_NewPersonRequiresExplicitRoleChoice()
    {
        await WithDb(async database =>
        {
            await WithFile("Given Name,Surname\nAna,Cruz\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);
                await viewModel.LoadFileAsync(path);
                var item = Assert.Single(viewModel.PersonReview);
                Assert.Equal(PersonImportResolutionStatus.NeedsReview.ToString(), item.Status);
            });
        });
    }

    [Fact]
    public async Task RegisterSafePeople_AddsNewPersonWithoutOverwritingExistingProfiles()
    {
        await WithDb(async database =>
        {
            await WithFile("External ID,Given Name,Surname,Envelope No\nX1,Ana,Cruz,M100\n", async path =>
            {
                var viewModel = new ImportWorkspaceViewModel();
                await viewModel.InitializeAsync(database);
                await viewModel.LoadFileAsync(path);
                viewModel.SelectedPersonDefaultRole = PersonImportDefaultRole.Member;
                await viewModel.RefreshPeopleReviewCommand.ExecuteAsync(null);
                await viewModel.RegisterSafePeopleCommand.ExecuteAsync(null);
                var people = await new SqlitePeopleGivingStore(database).GetPeopleAsync();
                var person = Assert.Single(people);
                Assert.Equal("Ana", person.FirstName);
                Assert.Equal("M100", person.MemberNumber);
            });
        });
    }

    [Fact]
    public void ImportView_OffersTemplatesWithoutForcingThem()
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "ImportWorkspaceView.xaml"));
        Assert.Contains("Templates and source profiles (optional)", xaml, StringComparison.Ordinal);
        Assert.Contains("Save Blank Template Copy", xaml, StringComparison.Ordinal);
        Assert.Contains("bring an existing CSV/XLS/XLSX", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportView_ExplainsConservativeIdentityResolution()
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "ChurchBooks.App", "Views", "ImportWorkspaceView.xaml"));
        Assert.Contains("Name-only matches require review", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Register Safe People", xaml, StringComparison.Ordinal);
        Assert.Contains("never posts automatically", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never merges people automatically", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MainWindowViewModel_ReportsSchemaNineOrLater()
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
            Assert.True(schemaVersion >= 9);
            Assert.Contains("adaptive Smart Import", status, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static async Task WithDb(Func<ChurchBooksDatabase, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase9.AppTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(root, "test.db"));
            await database.InitializeAsync();
            await action(database);
        }
        finally { await DeleteDirectoryWithRetryAsync(root); }
    }

    private static async Task WithFile(string content, Func<string, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.Phase9.AppFiles", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "people.csv");
            await File.WriteAllTextAsync(path, content);
            await action(path);
        }
        finally { await DeleteDirectoryWithRetryAsync(root); }
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
