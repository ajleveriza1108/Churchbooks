namespace ChurchBooks.Accounting.Importing;

public sealed record PersonImportApplyResult(int RegisteredCount, int LinkedExistingCount, int ReviewRequiredCount);
