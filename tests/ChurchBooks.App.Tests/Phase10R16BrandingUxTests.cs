using System.IO;
using ChurchBooks.App.Models;
using ChurchBooks.App.Preferences;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R16BrandingUxTests
{
    [Fact]
    public void MainWindow_UsesChurchBooksProductLogoAndSeparateOrganizationBranding()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("ChurchBooks-AppIcon-256.png", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Church\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Books\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OrganizationLogoPath", xaml, StringComparison.Ordinal);
        Assert.Contains("RemoveOrganizationLogoCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AppProject_UsesCbDerivedWindowsIconResources()
    {
        var project = ReadSource("src", "ChurchBooks.App", "ChurchBooks.App.csproj");
        Assert.Contains("ChurchBooks.ico", project, StringComparison.Ordinal);
        Assert.Contains("ChurchBooks-AppIcon-256.png", project, StringComparison.Ordinal);
    }

    [Fact]
    public void ServicesAndGiving_HaveIndependentNavigationState()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("ShowServicesCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("IsServicesVisible", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowGivingCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("IsGivingVisible", xaml, StringComparison.Ordinal);
        Assert.Contains("ServicesWorkspaceView", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeCatalog_ContainsLightWhiteAndNonAmoledDarkOptions()
    {
        var catalog = ReadSource("src", "ChurchBooks.App", "Models", "AppearanceTheme.cs");
        var theme = ReadSource("src", "ChurchBooks.App", "Services", "ThemeManager.cs");

        Assert.Contains("ChurchBooksLight", catalog, StringComparison.Ordinal);
        Assert.Contains("ClassicWhite", catalog, StringComparison.Ordinal);
        Assert.Contains("ChurchBooksDark", catalog, StringComparison.Ordinal);
        Assert.Contains("AppearanceTheme.ClassicWhite", theme, StringComparison.Ordinal);
        Assert.Contains("AppearanceTheme.ChurchBooksDark", theme, StringComparison.Ordinal);
        Assert.Contains("#F7F7F3", theme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#111827", theme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#000000", theme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UiPreferenceStore_RoundTripsThemeAndOrganizationLogoPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.R16.UiPrefs", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "preferences.json");
        try
        {
            var store = new UiPreferenceStore(path);
            var expected = new UiPreferences(WorkspaceMode.Accountant, HelpLevel.Guided)
            {
                AppearanceTheme = AppearanceTheme.ChurchBooksDark,
                OrganizationLogoPath = @"C:\Church\logo.png"
            };
            store.Save(expected);
            Assert.Equal(expected, store.Load());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReportsAndAudit_UseConsistentCenterNames()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Reports Center", xaml, StringComparison.Ordinal);
        Assert.Contains("Audit &amp; Integrity", xaml, StringComparison.Ordinal);
        Assert.Contains("Advanced Review Tools", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupView_UsesReadableTerminologyAndConditionalCompletion()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "SetupWorkspaceView.xaml");
        Assert.Contains("DisplayKey", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowCompleteSetupAction", xaml, StringComparison.Ordinal);
        Assert.Contains("Organization &amp; Terminology", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountingSafetyContracts_RemainExplicitInUpdatedUx()
    {
        var expenses = ReadSource("src", "ChurchBooks.App", "Views", "ExpensesWorkspaceView.xaml");
        var import = ReadSource("src", "ChurchBooks.App", "Views", "ImportWorkspaceView.xaml");
        Assert.Contains("Smart Import never posts expenses automatically", expenses, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bills/payables are intentionally reserved for the next phase", expenses, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never posts automatically", import, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never merges people automatically", import, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
