using ChurchBooks.Accounting.ChartOfAccounts;

namespace ChurchBooks.Accounting.Funds;

public sealed record FundActivityLine(
    Guid JournalEntryId,
    Guid JournalLineId,
    string EntryNumber,
    DateOnly PostingDate,
    Guid FundId,
    Guid AccountId,
    AccountType AccountType,
    decimal Debit,
    decimal Credit,
    string Description,
    string Reference,
    string Memo)
{
    public decimal NetAssetImpact => AccountType is AccountType.Asset or AccountType.Liability
        ? Debit - Credit
        : 0m;
}
