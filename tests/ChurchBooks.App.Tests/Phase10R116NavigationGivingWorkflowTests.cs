using System.IO;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R116NavigationGivingWorkflowTests
{
    [Fact]
    public void Shell_HasGlobalBackAndForwardNavigationButtons()
    {
        var shell = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("NavigateBackCommand", shell, StringComparison.Ordinal);
        Assert.Contains("NavigateForwardCommand", shell, StringComparison.Ordinal);
        Assert.Contains("CanNavigateBack", shell, StringComparison.Ordinal);
        Assert.Contains("CanNavigateForward", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationHistory_TracksSectionsAndRefreshesHistoryTargets()
    {
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("_backHistory", vm, StringComparison.Ordinal);
        Assert.Contains("_forwardHistory", vm, StringComparison.Ordinal);
        Assert.Contains("OnCurrentSectionChanging", vm, StringComparison.Ordinal);
        Assert.Contains("RefreshHistoryTargetAsync", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void DonorMemberDeepLink_OpensMemberManagementTab()
    {
        var shell = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("SelectedIndex=\"{Binding PeopleTabIndex}\"", shell, StringComparison.Ordinal);
        Assert.Contains("PeopleTabIndex = 0", vm, StringComparison.Ordinal);
        Assert.Contains("ShowMembersAsync", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void GivingCategoryDeepLink_OpensGivingSetupTab()
    {
        var giving = ReadSource("src", "ChurchBooks.App", "Views", "GivingWorkspaceView.xaml");
        var shell = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("ShowGivingSetupCommand", giving, StringComparison.Ordinal);
        Assert.Contains("SelectedIndex=\"{Binding SettingsTabIndex}\"", shell, StringComparison.Ordinal);
        Assert.Contains("SettingsTabIndex = 1", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchiveAction_KeepsArchivedPersonVisibleAndRestorable()
    {
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "PeopleGivingWorkspaceViewModel.cs");
        Assert.Contains("PersonArchiveActionLabel", vm, StringComparison.Ordinal);
        Assert.Contains("IncludeArchived = true", vm, StringComparison.Ordinal);
        Assert.Contains("SelectedPerson = People.FirstOrDefault", vm, StringComparison.Ordinal);
        Assert.Contains("restored to Active", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void MemberActions_FitOneLineAndPermanentDeleteUsesTrashIcon()
    {
        var shell = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Delete unused member or donor", shell, StringComparison.Ordinal);
        Assert.Contains("&#xE74D;", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Delete Unused\"", shell, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding PeopleWorkspace.PersonArchiveActionLabel}\"", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void GivingRows_AutoSynchronizeWithActiveCategoriesAndCanBeReordered()
    {
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "GivingWorkspaceViewModel.cs");
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "GivingWorkspaceView.xaml");
        Assert.Contains("SynchronizeBreakdownLinesWithCategories", vm, StringComparison.Ordinal);
        Assert.Contains("MoveBreakdownLineUp", vm, StringComparison.Ordinal);
        Assert.Contains("MoveBreakdownLineDown", vm, StringComparison.Ordinal);
        Assert.Contains("MoveBreakdownLineUpCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("MoveBreakdownLineDownCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankAutomaticGivingRows_AreIgnoredInsteadOfBlockingSave()
    {
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "GivingWorkspaceViewModel.cs");
        Assert.Contains("var enteredLines = BreakdownLines", vm, StringComparison.Ordinal);
        Assert.Contains("line.Fund is not null || !string.IsNullOrWhiteSpace(line.AmountText)", vm, StringComparison.Ordinal);
        Assert.Contains("Enter an amount for at least one giving category.", vm, StringComparison.Ordinal);
        Assert.Contains("var lines = enteredLines.Select", vm, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
