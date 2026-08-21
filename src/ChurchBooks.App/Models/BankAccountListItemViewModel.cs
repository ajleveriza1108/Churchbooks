using ChurchBooks.Accounting.Banking;
namespace ChurchBooks.App.Models;
public sealed record BankAccountListItemViewModel(BankAccount Account, decimal BookBalance)
{
    public string Name => Account.Name;
    public string Institution => Account.InstitutionName;
    public string MaskedNumber => string.IsNullOrWhiteSpace(Account.AccountLastFour) ? "" : "•••• " + Account.AccountLastFour;
    public string Status => Account.Status.ToString();
    public string BalanceDisplay => $"{Account.Currency.Value} {BookBalance:N2}";
}
