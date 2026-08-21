using System.IO;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R18OneScreenContrastTests
{
    [Fact]
    public void Dashboard_IsOneScreenAndContainsNoDashboardScrollViewer()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        var start = xaml.IndexOf("<Grid Visibility=\"{Binding IsDashboardVisible", StringComparison.Ordinal);
        var end = xaml.IndexOf("<Grid Visibility=\"{Binding IsPeopleVisible", start + 1, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var dashboard = xaml[start..end];
        Assert.DoesNotContain("<ScrollViewer", dashboard, StringComparison.Ordinal);
        Assert.Contains("Dashboard is intentionally a one-screen workspace", dashboard, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_UsesAdaptiveRowsThatFitSupportedMinimumWindow()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("MinWidth=\"1180\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"700\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"1.05*\" MinHeight=\"162\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"*\" MinHeight=\"150\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<RowDefinition Height=\"100\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<RowDefinition Height=\"5\" />", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_CompactStylesPreserveAllApprovedPanels()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("<Setter Property=\"Padding\" Value=\"10\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("DashboardSectionTitleStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground\" Value=\"{DynamicResource TextPrimaryBrush}", xaml, StringComparison.Ordinal);
        foreach (var title in new[] { "Service Financial Report", "Giving by Category", "Quick Actions", "Bank Accounts", "Recent Transactions", "Fund Balances", "BIR Readiness", "System Alerts &amp; Reminders" })
        {
            Assert.Contains(title, xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SharedTextDefaultsNeverFallBackToBlackInDarkMode()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Themes", "Controls.xaml");
        Assert.Contains("<Style TargetType=\"TextBlock\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<Style TargetType=\"Label\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Foreground\" Value=\"{DynamicResource TextPrimaryBrush}\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("WPF TextBlock does not reliably inherit Window.Foreground", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ComboBoxesUseChurchBooksTemplateAndDarkPopupSurface()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Themes", "Controls.xaml");
        Assert.Contains("x:Name=\"PART_Popup\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource SurfaceRaisedBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextElement.Foreground=\"{TemplateBinding Foreground}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsDropDownOpen", xaml, StringComparison.Ordinal);
        Assert.Contains("ComboBoxItem", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DatePickerInnerTextBoxFollowsSemanticTheme()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Themes", "Controls.xaml");
        Assert.Contains("primitives:DatePickerTextBox", xaml, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"{DynamicResource InputBrush}", xaml, StringComparison.Ordinal);
        Assert.Contains("CaretBrush\" Value=\"{DynamicResource AccentBrush}", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void R18OneScreenContractRemainsLockedAcrossLaterSubphases()
    {
        var manifest = ReadSource("churchbooks.manifest.json");
        Assert.Contains("\"oneScreenDashboardHardening\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"dashboardScrollViewerRemoved\": true", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"noDashboardOverlapCropOrHiddenControls\": true", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"supportedMinimumWindow\": \"1180x700\"", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"accountingBehaviorChanged\": false", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"schemaChanged\": false", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"licensingChanged\": false", manifest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingFailClosedAccountingAndImportBoundariesRemainIntact()
    {
        var manifest = ReadSource("churchbooks.manifest.json");
        Assert.Contains("\"automaticPosting\": false", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"automaticVendorCreation\": false", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"automaticAdjustmentJournals\": false", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"licensingImplemented\": false", manifest, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
