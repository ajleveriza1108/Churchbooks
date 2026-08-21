using System.IO;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R112DirectoryDateGridUxTests
{
    [Fact]
    public void Members_HasWorkingNavigationEditArchiveAndDeleteUnusedActions()
    {
        var shell = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "MainWindowViewModel.cs");
        Assert.Contains("PeopleWorkspace.EditPersonCommand", shell, StringComparison.Ordinal);
        Assert.Contains("PeopleWorkspace.DeleteUnusedPersonCommand", shell, StringComparison.Ordinal);
        Assert.Contains("ShowMembersAsync", vm, StringComparison.Ordinal);
        Assert.Contains("await ShowPeopleAsync()", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void Members_ExposesMiddleOrSecondGivenName()
    {
        var shell = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "PeopleGivingWorkspaceViewModel.cs");
        Assert.Contains("Middle / second given name", shell, StringComparison.Ordinal);
        Assert.Contains("PersonMiddleName", vm, StringComparison.Ordinal);
        Assert.Contains("middleName: draft.MiddleName", vm, StringComparison.Ordinal);
    }

    [Fact]
    public void PermanentDelete_IsRestrictedToUnusedPeople()
    {
        var service = ReadSource("src", "ChurchBooks.Accounting", "Services", "PeopleGivingManagementService.cs");
        var store = ReadSource("src", "ChurchBooks.Data", "Storage", "SqlitePeopleGivingStore.cs");
        Assert.Contains("DeleteUnusedPersonAsync", service, StringComparison.Ordinal);
        Assert.Contains("Use Archive instead", service, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM person_profiles", store, StringComparison.Ordinal);
        Assert.Contains("SqliteErrorCode == 19", store, StringComparison.Ordinal);
        Assert.Contains("historical giving or import records", store, StringComparison.Ordinal);
    }

    [Fact]
    public void UserFacingCodeLabels_AreRemovedFromXaml()
    {
        var files = new[]
        {
            ReadSource("src", "ChurchBooks.App", "MainWindow.xaml"),
            ReadSource("src", "ChurchBooks.App", "Views", "ExpensesWorkspaceView.xaml"),
            ReadSource("src", "ChurchBooks.App", "Views", "BankingWorkspaceView.xaml")
        };
        Assert.All(files, text => Assert.DoesNotContain("Header=\"Code\"", text, StringComparison.Ordinal));
        Assert.All(files, text => Assert.DoesNotContain("Text=\"Code\"", text, StringComparison.Ordinal));
        Assert.All(files, text => Assert.DoesNotContain("Fund code", text, StringComparison.OrdinalIgnoreCase));
        Assert.All(files, text => Assert.DoesNotContain("Ledger code", text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VendorExpense_HasReceiptInvoiceReferenceField()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "ExpensesWorkspaceView.xaml");
        var vm = ReadSource("src", "ChurchBooks.App", "ViewModels", "ExpensesWorkspaceViewModel.cs");
        Assert.Contains("Receipt / invoice / reference no.", xaml, StringComparison.Ordinal);
        Assert.Contains("receipt / invoice / reference number", vm, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dates_UseMmDdYyyyAutoSlashBehavior()
    {
        var behavior = ReadSource("src", "ChurchBooks.App", "Behaviors", "DateInputBehavior.cs");
        var controls = ReadSource("src", "ChurchBooks.App", "Themes", "Controls.xaml");
        Assert.Contains("MM/dd/yyyy", behavior, StringComparison.Ordinal);
        Assert.Contains("FormatDigits", behavior, StringComparison.Ordinal);
        Assert.Contains("DateInputBehavior.IsEnabled", controls, StringComparison.Ordinal);
        Assert.Contains("CalendarDayButton", controls, StringComparison.Ordinal);
    }

    [Fact]
    public void Tables_RemainSpreadsheetLikeWithoutBypassingAccountingValidation()
    {
        var controls = ReadSource("src", "ChurchBooks.App", "Themes", "Controls.xaml");
        Assert.Contains("CanUserReorderColumns\" Value=\"True", controls, StringComparison.Ordinal);
        Assert.Contains("CanUserResizeColumns\" Value=\"True", controls, StringComparison.Ordinal);
        Assert.Contains("CanUserSortColumns\" Value=\"True", controls, StringComparison.Ordinal);
        Assert.Contains("ClipboardCopyMode\" Value=\"IncludeHeader", controls, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly\" Value=\"True", controls, StringComparison.Ordinal);
    }

    [Fact]
    public void InternalCodes_AreGeneratedWithoutUserFacingCodeFields()
    {
        var shellVm = ReadSource("src", "ChurchBooks.App", "ViewModels", "MainWindowViewModel.cs");
        var peopleVm = ReadSource("src", "ChurchBooks.App", "ViewModels", "PeopleGivingWorkspaceViewModel.cs");
        var expensesVm = ReadSource("src", "ChurchBooks.App", "ViewModels", "ExpensesWorkspaceViewModel.cs");
        var bankingVm = ReadSource("src", "ChurchBooks.App", "ViewModels", "BankingWorkspaceViewModel.cs");
        Assert.Contains("BuildInternalCode(\"FUND\"", shellVm, StringComparison.Ordinal);
        Assert.Contains("BuildInternalCode(\"GIVE\"", peopleVm, StringComparison.Ordinal);
        Assert.Contains("vendorCode", expensesVm, StringComparison.Ordinal);
        Assert.Contains("BANK-", bankingVm, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
