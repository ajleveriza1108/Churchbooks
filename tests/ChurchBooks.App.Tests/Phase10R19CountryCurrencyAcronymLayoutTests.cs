using System.IO;
using ChurchBooks.App.Services;
using ChurchBooks.App.ViewModels;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R19CountryCurrencyAcronymLayoutTests
{
    [Fact]
    public void CountryCatalog_ContainsDeterministicPrimaryCurrencies()
    {
        Assert.Equal("PHP", CountryCurrencyCatalog.CurrencyForCountry("PH"));
        Assert.Equal("USD", CountryCurrencyCatalog.CurrencyForCountry("US"));
        Assert.Equal("GBP", CountryCurrencyCatalog.CurrencyForCountry("GB"));
        Assert.Equal("JPY", CountryCurrencyCatalog.CurrencyForCountry("JP"));
    }

    [Fact]
    public void SelectingCountry_ImmediatelySuggestsItsBaseCurrency()
    {
        var viewModel = new SetupWorkspaceViewModel();
        viewModel.CountryCode = "US";
        Assert.Equal("USD", viewModel.BaseCurrency);
        viewModel.CountryCode = "PH";
        Assert.Equal("PHP", viewModel.BaseCurrency);
    }

    [Fact]
    public void SuggestedBaseCurrency_RemainsUserOverridableUntilCountryChangesAgain()
    {
        var viewModel = new SetupWorkspaceViewModel();
        viewModel.CountryCode = "US";
        viewModel.BaseCurrency = "CAD";
        Assert.Equal("CAD", viewModel.BaseCurrency);
        viewModel.CountryCode = "GB";
        Assert.Equal("GBP", viewModel.BaseCurrency);
    }

    [Fact]
    public void OrganizationAcronym_UsesInitialsForLongChurchNames()
    {
        var viewModel = new SetupWorkspaceViewModel
        {
            OrganizationDisplayName = "Bible Fellowship Baptist Church"
        };
        Assert.Equal("BFBC", viewModel.OrganizationAcronym);
        viewModel.OrganizationDisplayName = "Grace Community Church";
        Assert.Equal("GCC", viewModel.OrganizationAcronym);
    }

    [Fact]
    public void OrganizationAcronym_PreservesAlreadyCompactSingleWordNames()
    {
        var viewModel = new SetupWorkspaceViewModel
        {
            OrganizationDisplayName = "BFBC"
        };
        Assert.Equal("BFBC", viewModel.OrganizationAcronym);
    }

    [Fact]
    public void Settings_UsesCountryAndCurrencySelectorsInsteadOfFreeTextCountryEntry()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "Views", "SetupWorkspaceView.xaml");
        Assert.Contains("ItemsSource=\"{Binding CountryOptions}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedValuePath=\"CountryCode\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CurrencyCodes}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Selecting a country automatically chooses its standard base currency.", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TopOrganizationSelector_UsesAcronymAndRetainsFullNameAsTooltip()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("<ColumnDefinition Width=\"150\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SetupWorkspace.OrganizationAcronym}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding SetupWorkspace.OrganizationDisplayName}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SidebarOrganizationCard_AutoSizesSoSettingsButtonCannotBeClippedByFixedCardHeight()
    {
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");
        Assert.Contains("<RowDefinition Height=\"70\" />\n                    <RowDefinition Height=\"*\" />\n                    <RowDefinition Height=\"Auto\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Organization Settings\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"36\"", xaml, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
