namespace ChurchBooks.Accounting.Setup;

public sealed class TerminologyCatalog
{
    private static readonly IReadOnlyDictionary<string, TerminologyDefinition> Defaults =
        new Dictionary<string, TerminologyDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [TerminologyKeys.Member] = new(TerminologyKeys.Member, "Member", "Members"),
            [TerminologyKeys.Donor] = new(TerminologyKeys.Donor, "Donor", "Donors"),
            [TerminologyKeys.Household] = new(TerminologyKeys.Household, "Household", "Households"),
            [TerminologyKeys.Service] = new(TerminologyKeys.Service, "Service", "Services"),
            [TerminologyKeys.Offering] = new(TerminologyKeys.Offering, "Offering", "Offerings"),
            [TerminologyKeys.GivingCategory] = new(TerminologyKeys.GivingCategory, "Giving Category", "Giving Categories"),
            [TerminologyKeys.Fund] = new(TerminologyKeys.Fund, "Fund", "Funds"),
            [TerminologyKeys.BankAccount] = new(TerminologyKeys.BankAccount, "Bank Account", "Bank Accounts"),
            [TerminologyKeys.Deposit] = new(TerminologyKeys.Deposit, "Deposit", "Deposits"),
            [TerminologyKeys.Ministry] = new(TerminologyKeys.Ministry, "Ministry", "Ministries")
        };

    private readonly IReadOnlyDictionary<string, TerminologyDefinition> _terms;

    public TerminologyCatalog(IEnumerable<TerminologyDefinition>? overrides = null)
    {
        var terms = new Dictionary<string, TerminologyDefinition>(Defaults, StringComparer.OrdinalIgnoreCase);
        if (overrides is not null)
        {
            foreach (var item in overrides) terms[item.Key] = item;
        }
        _terms = terms;
    }

    public IReadOnlyList<TerminologyDefinition> Terms => TerminologyKeys.All.Select(Get).ToArray();
    public TerminologyDefinition Get(string key) => _terms.TryGetValue(key, out var item) ? item : new TerminologyDefinition(key, key, key + "s");
    public string Singular(string key) => Get(key).Singular;
    public string Plural(string key) => Get(key).Plural;
}
