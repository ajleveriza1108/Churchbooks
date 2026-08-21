namespace ChurchBooks.Accounting.Reconciliation;

public sealed record ReconciliationBookItem(
    Guid JournalEntryId,
    string EntryNumber,
    DateOnly PostingDate,
    decimal Amount,
    string Description,
    string Reference);
