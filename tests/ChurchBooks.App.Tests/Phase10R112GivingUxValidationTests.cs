using System.IO;
using ChurchBooks.App.Behaviors;
using ChurchBooks.App.Converters;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R112GivingUxValidationTests
{
    [Fact]
    public void GivingEntry_ReferenceAndMemoAreExplicitlyOptional()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "GivingWorkspaceView.xaml");
        Assert.Contains("Reference &amp; Memo (Optional)", xaml, StringComparison.Ordinal);
        Assert.Contains("Envelope / receipt / reference (optional)", xaml, StringComparison.Ordinal);
        Assert.Contains("Memo (optional)", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void GivingEntry_ServiceReferenceAndDonorMemoUseMatchingGridRows()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "GivingWorkspaceView.xaml");
        Assert.Contains("Grid.Row=\"1\" Margin=\"0,10,0,5\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"3\" Margin=\"0,12,0,5\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"4\" Text=\"{Binding ContributionMemo", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BreakdownRows_HaveNamedColumnsTooltipsSetupNavigationAndReorderControls()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "GivingWorkspaceView.xaml");
        Assert.Contains(">Giving Category</Hyperlink>", xaml, StringComparison.Ordinal);
        Assert.Contains(">Fund</Hyperlink>", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Amount\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Order\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MoveBreakdownLineUpCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("MoveBreakdownLineDownCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowGivingSetupCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowFundsCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowServicesCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowMembersCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void GivingCategories_AutomaticallyCreateRowsWithoutAnAddBreakdownButton()
    {
        var source = ReadSource("src", "ChurchBooks.App", "ViewModels", "GivingWorkspaceViewModel.cs");
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "GivingWorkspaceView.xaml");
        Assert.Contains("SynchronizeBreakdownLinesWithCategories", source, StringComparison.Ordinal);
        Assert.Contains("CreateBreakdownLine(category)", source, StringComparison.Ordinal);
        Assert.Contains("GivingCategory = category, Fund = null, AmountText = string.Empty", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Add Breakdown Row", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AddBreakdownLineCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountingAmount_AcceptsThousandsCommaAndOneDecimalPoint()
    {
        Assert.True(AccountingAmountTextBoxBehavior.IsPotentialAmountText("1,234.56"));
        Assert.True(AccountingAmountTextBoxBehavior.TryParseAmount("1,234.56", out var amount));
        Assert.Equal(1234.56m, amount);
    }

    [Fact]
    public void AccountingAmount_RejectsLettersCurrencySymbolsAndExtraDecimalPrecision()
    {
        Assert.False(AccountingAmountTextBoxBehavior.IsPotentialAmountText("PHP 1,234.56"));
        Assert.False(AccountingAmountTextBoxBehavior.IsPotentialAmountText("1,234.5.6"));
        Assert.False(AccountingAmountTextBoxBehavior.IsPotentialAmountText("1,234.567"));
        Assert.False(AccountingAmountTextBoxBehavior.IsPotentialAmountText("1,23a.45"));
    }

    [Fact]
    public void AccountingAmount_FormatsAsXCommaXxxDotXx()
    {
        Assert.Equal("1,234.50", AccountingAmountTextBoxBehavior.FormatAmount(1234.5m));
        Assert.Equal("1,000,000.00", AccountingAmountTextBoxBehavior.FormatAmount(1000000m));
    }

    [Fact]
    public void PersonName_RejectsDigitsAndTitleCasesAllGivenNames()
    {
        Assert.False(PersonNameTextBoxBehavior.IsAllowedNameText("Alvin2"));
        Assert.True(PersonNameTextBoxBehavior.IsAllowedNameText("alvin joseph"));
        Assert.Equal("Alvin Joseph", PersonNameTextBoxBehavior.NormalizeName("alvin joseph"));
        Assert.Equal("Mary-Jane O'Connor", PersonNameTextBoxBehavior.NormalizeName("mary-jane o'connor"));
    }

    [Fact]
    public void DonorDisplay_PreservesAllGivenNamesBeforeLastName()
    {
        var converter = new PersonFullNameConverter();
        var value = converter.Convert(new PersonStub { FirstName = "alvin joseph", LastName = "tan" }, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Alvin Joseph Tan", value);
        var givingXaml = ReadSource("src", "ChurchBooks.App", "Views", "GivingWorkspaceView.xaml");
        Assert.DoesNotContain("DisplayMemberPath=\"DisplayName\"", givingXaml, StringComparison.Ordinal);
        var shellXaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Binding=\"{Binding Converter={StaticResource PersonFullNameConverter}}\"", shellXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void GivingEntry_ListsOnlyOpenBatchesAndDoesNotAutoSelectFirstDonor()
    {
        var source = ReadSource("src", "ChurchBooks.App", "ViewModels", "GivingWorkspaceViewModel.cs");
        Assert.Contains("batch.Status == OfferingBatchStatus.Open", source, StringComparison.Ordinal);
        Assert.Contains("person.IsDonor || person.IsMember", source, StringComparison.Ordinal);
        Assert.Contains(": null;", source, StringComparison.Ordinal);
        Assert.DoesNotContain(": Donors.FirstOrDefault();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GivingCategoryConfiguration_LivesInSettingsNotMemberRegistrationTabs()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("Header=\"Giving Setup\"", xaml, StringComparison.Ordinal);
        Assert.Contains("configured before entry", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"{Binding SetupWorkspace.GivingCategoryPlural}\">", xaml, StringComparison.Ordinal);
        Assert.Contains("Middle / second given name", xaml, StringComparison.Ordinal);
        Assert.Contains("PersonNameTextBoxBehavior.IsEnabled=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PeopleWorkspace.EditPersonCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Edit\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Load the selected member/donor into the editor.", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_LocksGivingValidationSafetyAndSourceOnlyRepositoryPublication()
    {
        var manifest = ReadSource("churchbooks.manifest.json");
        Assert.Contains("\"subphase\": \"R1.12\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"amountFormat\": \"x,xxx.xx\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"amountAcceptsThousandsComma\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"allGivenNamesPreservedInDonorDisplay\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"givingDirectoryIncludesMembers\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"givingDirectoryRefreshesOnOpen\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"personEditCommandExposed\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"schemaChanged\": false", manifest, StringComparison.Ordinal);
        Assert.Contains("\"automaticPosting\": false", manifest, StringComparison.Ordinal);
        Assert.Contains("\"sourceOnly\": true", manifest, StringComparison.Ordinal);
        Assert.Contains("\"forcePush\": false", manifest, StringComparison.Ordinal);
    }

    private sealed class PersonStub
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
