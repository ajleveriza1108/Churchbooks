using ChurchBooks.Accounting.Funds;

namespace ChurchBooks.Accounting.Reporting;

public sealed class RestrictionReleaseReviewService
{
    public RestrictionReleaseReview Review(
        Fund fund,
        decimal availableFundBalance,
        decimal requestedAmount,
        string? evidenceNote)
    {
        ArgumentNullException.ThrowIfNull(fund);

        if (fund.Restriction is FundRestriction.Unrestricted or FundRestriction.BoardDesignated)
        {
            return new RestrictionReleaseReview(
                RestrictionReleaseReviewStatus.NotApplicable,
                requestedAmount,
                availableFundBalance,
                "This fund is not donor restricted. A restriction-release entry is not applicable.");
        }

        if (requestedAmount <= 0m)
        {
            return new RestrictionReleaseReview(
                RestrictionReleaseReviewStatus.InvalidAmount,
                requestedAmount,
                availableFundBalance,
                "Enter a release amount greater than zero.");
        }

        if (requestedAmount > availableFundBalance)
        {
            return new RestrictionReleaseReview(
                RestrictionReleaseReviewStatus.InsufficientFundBalance,
                requestedAmount,
                availableFundBalance,
                "The requested release exceeds the fund balance available in ChurchBooks.");
        }

        if (fund.Restriction == FundRestriction.Endowment)
        {
            return new RestrictionReleaseReview(
                RestrictionReleaseReviewStatus.EndowmentSpecialReviewRequired,
                requestedAmount,
                availableFundBalance,
                "Endowment releases require dedicated policy and accountant review. ChurchBooks will not create a journal automatically.");
        }

        if (string.IsNullOrWhiteSpace(evidenceNote))
        {
            return new RestrictionReleaseReview(
                RestrictionReleaseReviewStatus.EvidenceRequired,
                requestedAmount,
                availableFundBalance,
                "Document how the donor restriction was satisfied before requesting an accounting release.");
        }

        return new RestrictionReleaseReview(
            RestrictionReleaseReviewStatus.AccountantReviewRequired,
            requestedAmount,
            availableFundBalance,
            "The request is ready for accountant review. Phase 3D does not post a restriction-release journal automatically.");
    }
}
