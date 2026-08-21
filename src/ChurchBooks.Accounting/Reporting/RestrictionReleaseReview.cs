namespace ChurchBooks.Accounting.Reporting;

public sealed record RestrictionReleaseReview(
    RestrictionReleaseReviewStatus Status,
    decimal RequestedAmount,
    decimal AvailableFundBalance,
    string Message)
{
    public bool IsReadyForReview => Status == RestrictionReleaseReviewStatus.AccountantReviewRequired;
    public bool PostsJournal => false;
}
