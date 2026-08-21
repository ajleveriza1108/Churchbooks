namespace ChurchBooks.Accounting.Importing;

public enum PersonImportResolutionStatus
{
    NewPerson = 0,
    ExistingExact = 1,
    NeedsReview = 2,
    Ambiguous = 3,
    Invalid = 4
}
