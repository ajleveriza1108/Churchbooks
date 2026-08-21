namespace ChurchBooks.Accounting.GeneralLedger;

public sealed record LedgerLine(
    Guid JournalEntryId,
    string EntryNumber,
    DateOnly PostingDate,
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string Description,
    string Reference,
    string Memo);
