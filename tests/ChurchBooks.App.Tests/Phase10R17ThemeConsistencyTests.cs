using System.IO;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R17ThemeConsistencyTests
{
    [Fact]
    public void ThemeManager_UpdatesBrushOwnersInsideMergedDictionaries()
    {
        var source = ReadSource("src", "ChurchBooks.App", "Services", "ThemeManager.cs");
        Assert.Contains("SetBrushRecursive", source, StringComparison.Ordinal);
        Assert.Contains("MergedDictionaries", source, StringComparison.Ordinal);
        Assert.Contains("ContainsLocalKey", source, StringComparison.Ordinal);
        Assert.Contains("styles inside another merged dictionary", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThemeTokens_DefineCompleteSemanticPalette()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Themes", "ThemeTokens.xaml");
        foreach (var key in new[]
        {
            "WindowBrush", "SurfaceBrush", "SurfaceMutedBrush", "SurfaceRaisedBrush",
            "ShellBrush", "ShellMutedBrush", "InputBrush", "HeaderBrush",
            "BorderBrush", "BorderStrongBrush", "TextPrimaryBrush", "TextSecondaryBrush", "TextDisabledBrush",
            "AccentBrush", "AccentHoverBrush", "AccentSoftBrush", "AccentContrastBrush",
            "SuccessSoftBrush", "WarningSoftBrush", "DangerSoftBrush", "TealSoftBrush", "PurpleSoftBrush",
            "SelectionBrush", "ScrollTrackBrush", "ScrollThumbBrush"
        })
        {
            Assert.Contains($"x:Key=\"{key}\"", xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SharedControls_AreThemeAwareAcrossTablesTabsInputsAndPopups()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Themes", "Controls.xaml");
        Assert.Contains("TargetType=\"DataGridRow\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"DataGridCell\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"ComboBoxItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"DatePicker\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"TabItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"ToolTip\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"ContextMenu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"ScrollBar\"", xaml, StringComparison.Ordinal);
        var chart = ReadSource("src", "ChurchBooks.App", "Controls", "OfferingChartControl.cs");
        Assert.Contains("ThemeBrush(\"BorderBrush\"", chart, StringComparison.Ordinal);
        Assert.Contains("ThemeBrush(\"TextPrimaryBrush\"", chart, StringComparison.Ordinal);
        Assert.Contains("ThemeBrush(\"SurfaceBrush\"", chart, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_UsesSemanticBrushesInsteadOfFixedLightAlertColors()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("SuccessSoftBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("WarningSoftBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("DangerSoftBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("TealSoftBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("PurpleSoftBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#E8F8F0", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#FFF0F2", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#E9F8F8", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#FFF4E7", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#F2ECFF", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#94A3B8", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductBrand_RemainsReadableInLightAndDarkThemes()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("ChurchBooks-AppIcon-256.png", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Church\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Books\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource TextPrimaryBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource AccentBrush}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_AppliesPersistedThemeAfterResourcesAreInitialized()
    {
        var source = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml.cs");
        var initializeIndex = source.IndexOf("InitializeComponent();", StringComparison.Ordinal);
        var applyIndex = source.IndexOf("ThemeManager.Apply(_viewModel.SelectedAppearanceTheme);", StringComparison.Ordinal);
        Assert.True(initializeIndex >= 0);
        Assert.True(applyIndex > initializeIndex);
        Assert.Contains("DwmwaUseImmersiveDarkMode", source, StringComparison.Ordinal);
        Assert.Contains("DwmSetWindowAttribute", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkPalette_UsesLayeredSlateSurfacesAndReadableText_NotAmoledBlack()
    {
        var source = ReadSource("src", "ChurchBooks.App", "Services", "ThemeManager.cs");
        Assert.Contains("#0F172A", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#162033", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#1D2A3D", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#F4F7FB", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#AAB4C3", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#000000", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellAndWorkspaceBackgrounds_UseThemeResourcesConsistently()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Background=\"{DynamicResource ShellBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource WindowBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource ShellMutedBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource AccentContrastBrush}\"", xaml, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
