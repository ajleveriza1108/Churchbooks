namespace ChurchBooks.Accounting.Reporting;

public enum RestrictionReleaseReviewStatus
{
    NotApplicable = 1,
    InvalidAmount = 2,
    InsufficientFundBalance = 3,
    EvidenceRequired = 4,
    AccountantReviewRequired = 5,
    EndowmentSpecialReviewRequired = 6
}
