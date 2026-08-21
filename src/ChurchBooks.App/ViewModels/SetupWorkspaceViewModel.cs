using System.Collections.ObjectModel;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Services;
using ChurchBooks.Accounting.Setup;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class SetupWorkspaceViewModel : ObservableObject
{
    private ISetupStore? _store;
    private FirstRunSetupService? _service;
    private bool _isApplyingProfile;

    public event EventHandler? TerminologyChanged;

    public ObservableCollection<TerminologyEditorItem> Terms { get; } = new();
    public ObservableCollection<CustomSearchAlias> CustomAliases { get; } = new();
    public IReadOnlyList<int> FiscalMonths { get; } = Enumerable.Range(1, 12).ToArray();
    public IReadOnlyList<CountryCurrencyOption> CountryOptions { get; } = CountryCurrencyCatalog.Options;
    public IReadOnlyList<string> CurrencyCodes { get; } = CountryCurrencyCatalog.CurrencyCodes;
    public IReadOnlyList<string> AliasAreas { get; } = new[] { "People", "Giving", "Funds", "Banking", "Reports", "Integrity", "Setup" };

    [ObservableProperty] private string _organizationDisplayName = "My Church";
    [ObservableProperty] private string _legalName = "My Church";
    [ObservableProperty] private string _baseCurrency = "PHP";
    [ObservableProperty] private int _fiscalYearStartMonth = 1;
    [ObservableProperty] private string _countryCode = "PH";
    [ObservableProperty] private string _taxIdentifier = string.Empty;
    [ObservableProperty] private bool _isSetupComplete;
    [ObservableProperty] private string _statusMessage = "Complete first-time setup to personalize ChurchBooks.";
    [ObservableProperty] private string _aliasArea = "People";
    [ObservableProperty] private string _aliasText = string.Empty;
    [ObservableProperty] private CustomSearchAlias? _selectedAlias;

    public string MemberSingular => Term(TerminologyKeys.Member).Singular;
    public string MemberPlural => Term(TerminologyKeys.Member).Plural;
    public string DonorSingular => Term(TerminologyKeys.Donor).Singular;
    public string DonorPlural => Term(TerminologyKeys.Donor).Plural;
    public string HouseholdSingular => Term(TerminologyKeys.Household).Singular;
    public string HouseholdPlural => Term(TerminologyKeys.Household).Plural;
    public string ServiceSingular => Term(TerminologyKeys.Service).Singular;
    public string ServicePlural => Term(TerminologyKeys.Service).Plural;
    public string OfferingSingular => Term(TerminologyKeys.Offering).Singular;
    public string OfferingPlural => Term(TerminologyKeys.Offering).Plural;
    public string GivingCategorySingular => Term(TerminologyKeys.GivingCategory).Singular;
    public string GivingCategoryPlural => Term(TerminologyKeys.GivingCategory).Plural;
    public string FundSingular => Term(TerminologyKeys.Fund).Singular;
    public string FundPlural => Term(TerminologyKeys.Fund).Plural;
    public string BankAccountSingular => Term(TerminologyKeys.BankAccount).Singular;
    public string BankAccountPlural => Term(TerminologyKeys.BankAccount).Plural;
    public string DepositSingular => Term(TerminologyKeys.Deposit).Singular;
    public string DepositPlural => Term(TerminologyKeys.Deposit).Plural;
    public bool ShowCompleteSetupAction => !IsSetupComplete;
    public string OrganizationAcronym => BuildOrganizationAcronym(OrganizationDisplayName);

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await new Phase6DatabaseMigrator(database).InitializeAsync(cancellationToken);

        _store = new SqliteSetupStore(database);
        var accountingStore = new SqliteAccountingStore(database);
        var peopleStore = new SqlitePeopleGivingStore(database);
        var bankingStore = new SqliteBankingStore(database);
        _service = new FirstRunSetupService(_store, accountingStore, peopleStore, bankingStore);

        var loaded = await _service.LoadAsync(cancellationToken);
        ApplyProfile(loaded.Profile);

        Terms.Clear();
        foreach (var term in loaded.Terminology.Terms)
        {
            var item = new TerminologyEditorItem { Key = term.Key, Singular = term.Singular, Plural = term.Plural };
            item.PropertyChanged += (_, _) => RaiseTermProperties();
            Terms.Add(item);
        }

        Replace(CustomAliases, loaded.Aliases);
        RaiseTermProperties();
    }

    [RelayCommand]
    private async Task SaveSetupAsync()
    {
        if (_service is null || _store is null) return;
        try
        {
            foreach (var item in Terms)
            {
                await _store.SaveTerminologyAsync(new TerminologyDefinition(item.Key, item.Singular, item.Plural));
            }
            await _store.SaveOrganizationProfileAsync(BuildProfile(IsSetupComplete));
            StatusMessage = "Personalization saved. You can change these terms later in Settings.";
            RaiseTermProperties();
            TerminologyChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CompleteSetupAsync()
    {
        if (_service is null) return;
        try
        {
            await _service.CompleteAsync(
                BuildProfile(true),
                Terms.Select(item => new TerminologyDefinition(item.Key, item.Singular, item.Plural)));
            IsSetupComplete = true;
            StatusMessage = "First-time setup complete. ChurchBooks is personalized and the starter accounting period/category mappings are ready.";
            RaiseTermProperties();
            TerminologyChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddAliasAsync()
    {
        if (_store is null || string.IsNullOrWhiteSpace(AliasText)) return;
        try
        {
            var alias = new CustomSearchAlias(Guid.NewGuid(), AliasArea, AliasText);
            await _store.AddCustomSearchAliasAsync(alias);
            CustomAliases.Add(alias);
            AliasText = string.Empty;
            StatusMessage = "Custom search term added.";
            TerminologyChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveAliasAsync()
    {
        if (_store is null || SelectedAlias is null) return;
        try
        {
            await _store.DeleteCustomSearchAliasAsync(SelectedAlias.Id);
            CustomAliases.Remove(SelectedAlias);
            SelectedAlias = null;
            StatusMessage = "Custom search term removed.";
            TerminologyChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public TerminologyCatalog GetCatalog() => new(
        Terms.Select(item => new TerminologyDefinition(
            item.Key,
            string.IsNullOrWhiteSpace(item.Singular) ? item.Key : item.Singular,
            string.IsNullOrWhiteSpace(item.Plural) ? item.Key + "s" : item.Plural)));

    private OrganizationProfile BuildProfile(bool complete) => new(
        string.IsNullOrWhiteSpace(OrganizationDisplayName) ? "My Church" : OrganizationDisplayName,
        string.IsNullOrWhiteSpace(LegalName) ? OrganizationDisplayName : LegalName,
        new CurrencyCode(string.IsNullOrWhiteSpace(BaseCurrency) ? "PHP" : BaseCurrency),
        FiscalYearStartMonth,
        string.IsNullOrWhiteSpace(CountryCode) ? "PH" : CountryCode,
        TaxIdentifier,
        complete);

    private void ApplyProfile(OrganizationProfile profile)
    {
        _isApplyingProfile = true;
        try
        {
            OrganizationDisplayName = profile.DisplayName;
            LegalName = profile.LegalName;
            FiscalYearStartMonth = profile.FiscalYearStartMonth;
            CountryCode = profile.CountryCode;
            BaseCurrency = profile.BaseCurrency.Value;
            TaxIdentifier = profile.TaxIdentifier;
            IsSetupComplete = profile.SetupComplete;
        }
        finally
        {
            _isApplyingProfile = false;
        }
    }

    partial void OnOrganizationDisplayNameChanged(string value)
    {
        OnPropertyChanged(nameof(OrganizationAcronym));
    }

    partial void OnCountryCodeChanged(string value)
    {
        if (_isApplyingProfile) return;

        var recommendedCurrency = CountryCurrencyCatalog.CurrencyForCountry(value);
        if (!string.IsNullOrWhiteSpace(recommendedCurrency) &&
            !string.Equals(BaseCurrency, recommendedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            BaseCurrency = recommendedCurrency;
            StatusMessage = $"Base currency changed to {recommendedCurrency} for the selected country. You can still override it before saving.";
        }
    }

    partial void OnIsSetupCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCompleteSetupAction));
    }

    private static string BuildOrganizationAcronym(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "CB";

        var words = System.Text.RegularExpressions.Regex.Matches(
                displayName.Trim(),
                @"[\p{L}\p{Nd}]+(?:['’][\p{L}\p{Nd}]+)*")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(match => match.Value)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .ToArray();

        if (words.Length == 0) return "CB";
        if (words.Length == 1)
        {
            var word = words[0];
            return word.Length <= 6
                ? word.ToUpperInvariant()
                : new string(word.Take(4).Select(char.ToUpperInvariant).ToArray());
        }

        return new string(words
            .Take(6)
            .Select(word => char.ToUpperInvariant(word[0]))
            .ToArray());
    }

    private TerminologyEditorItem Term(string key) =>
        Terms.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? new TerminologyEditorItem { Key = key, Singular = key, Plural = key + "s" };

    private void RaiseTermProperties()
    {
        foreach (var propertyName in new[]
        {
            nameof(MemberSingular), nameof(MemberPlural), nameof(DonorSingular), nameof(DonorPlural),
            nameof(HouseholdSingular), nameof(HouseholdPlural), nameof(ServiceSingular), nameof(ServicePlural),
            nameof(OfferingSingular), nameof(OfferingPlural), nameof(GivingCategorySingular), nameof(GivingCategoryPlural),
            nameof(FundSingular), nameof(FundPlural), nameof(BankAccountSingular), nameof(BankAccountPlural),
            nameof(DepositSingular), nameof(DepositPlural)
        })
        {
            OnPropertyChanged(propertyName);
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
