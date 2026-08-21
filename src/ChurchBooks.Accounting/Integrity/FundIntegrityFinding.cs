namespace ChurchBooks.Accounting.Integrity;

public sealed record FundIntegrityFinding(
    string Code,
    FundIntegritySeverity Severity,
    string Message,
    Guid? FundId = null,
    Guid? JournalEntryId = null);
