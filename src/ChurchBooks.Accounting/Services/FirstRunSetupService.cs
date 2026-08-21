using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Banking;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Periods;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.Setup;

namespace ChurchBooks.Accounting.Services;

public sealed class FirstRunSetupService
{
    private readonly ISetupStore _setupStore;
    private readonly IAccountingStore _accountingStore;
    private readonly IPeopleGivingStore _peopleStore;
    private readonly IBankingStore _bankingStore;

    public FirstRunSetupService(ISetupStore setupStore, IAccountingStore accountingStore, IPeopleGivingStore peopleStore, IBankingStore bankingStore)
    {
        _setupStore = setupStore ?? throw new ArgumentNullException(nameof(setupStore));
        _accountingStore = accountingStore ?? throw new ArgumentNullException(nameof(accountingStore));
        _peopleStore = peopleStore ?? throw new ArgumentNullException(nameof(peopleStore));
        _bankingStore = bankingStore ?? throw new ArgumentNullException(nameof(bankingStore));
    }

    public async Task<(OrganizationProfile Profile, TerminologyCatalog Terminology, IReadOnlyList<CustomSearchAlias> Aliases)> LoadAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _setupStore.GetOrganizationProfileAsync(cancellationToken)
            ?? new OrganizationProfile("My Church", "My Church", new ChurchBooks.Core.Finance.CurrencyCode("PHP"), 1, "PH");
        var terms = new TerminologyCatalog(await _setupStore.GetTerminologyOverridesAsync(cancellationToken));
        var aliases = await _setupStore.GetCustomSearchAliasesAsync(cancellationToken);
        return (profile, terms, aliases);
    }

    public async Task CompleteAsync(OrganizationProfile profile, IEnumerable<TerminologyDefinition> terminology, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(terminology);
        foreach (var term in terminology) await _setupStore.SaveTerminologyAsync(term, cancellationToken);

        var completed = new OrganizationProfile(profile.DisplayName, profile.LegalName, profile.BaseCurrency, profile.FiscalYearStartMonth,
            profile.CountryCode, profile.TaxIdentifier, setupComplete: true, updatedUtc: DateTimeOffset.UtcNow);
        await _setupStore.SaveOrganizationProfileAsync(completed, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (await _accountingStore.GetOpenPeriodForDateAsync(today, cancellationToken) is null)
        {
            var startYear = today.Month < completed.FiscalYearStartMonth ? today.Year - 1 : today.Year;
            var start = new DateOnly(startYear, completed.FiscalYearStartMonth, 1);
            var end = start.AddYears(1).AddDays(-1);
            await _accountingStore.AddPeriodAsync(new AccountingPeriod(Guid.NewGuid(), $"FY {start:yyyy-MM-dd} to {end:yyyy-MM-dd}", start, end), cancellationToken);
        }

        var incomeAccounts = await _accountingStore.GetAccountsByTypeAsync(AccountType.Income, cancellationToken);
        Account incomeAccount;
        if (incomeAccounts.Count == 0)
        {
            var code = await FindAvailableIncomeCodeAsync(cancellationToken);
            incomeAccount = new Account(Guid.NewGuid(), code, "Contribution Income", AccountType.Income);
            await _accountingStore.AddAccountAsync(incomeAccount, cancellationToken);
        }
        else
        {
            incomeAccount = incomeAccounts[0];
        }

        var mappings = await _bankingStore.GetGivingCategoryIncomeMappingsAsync(cancellationToken);
        var categories = await _peopleStore.GetGivingCategoriesAsync(cancellationToken: cancellationToken);
        foreach (var category in categories.Where(static x => x.Status == GivingCategoryStatus.Active))
        {
            if (!mappings.ContainsKey(category.Id))
                await _bankingStore.SetGivingCategoryIncomeMappingAsync(new GivingCategoryIncomeMapping(category.Id, incomeAccount.Id), cancellationToken);
        }
    }

    private async Task<string> FindAvailableIncomeCodeAsync(CancellationToken cancellationToken)
    {
        for (var code = 4000; code <= 4990; code += 10)
        {
            var text = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (await _accountingStore.GetAccountByCodeAsync(text, cancellationToken) is null) return text;
        }
        throw new InvalidOperationException("No available starter income account code was found between 4000 and 4990.");
    }
}
