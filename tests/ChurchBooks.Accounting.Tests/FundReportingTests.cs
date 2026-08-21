using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Reporting;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundReportingTests
{
    [Fact]
    public async Task BalanceReport_SummarizesRestrictedAndOtherBalances()
    {
        var general = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
        var missions = new Fund(Guid.NewGuid(), "MISSIONS", "Missions Fund", FundRestriction.DonorRestricted);
        var store = new ReportingStore(general, missions);
        store.AddActivity(general.Id, new DateOnly(2026, 8, 1), 300m);
        store.AddActivity(missions.Id, new DateOnly(2026, 8, 2), 200m);

        var report = await new FundReportingService(store).BuildBalanceReportAsync(new DateOnly(2026, 8, 31));

        Assert.Equal(500m, report.TotalBalance);
        Assert.Equal(200m, report.RestrictedBalance);
        Assert.Equal(300m, report.UnrestrictedAndDesignatedBalance);
    }

    [Fact]
    public async Task BalanceReport_AsOfDateExcludesFutureActivity()
    {
        var fund = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
        var store = new ReportingStore(fund);
        store.AddActivity(fund.Id, new DateOnly(2026, 8, 1), 100m);
        store.AddActivity(fund.Id, new DateOnly(2026, 9, 1), 250m);

        var report = await new FundReportingService(store).BuildBalanceReportAsync(new DateOnly(2026, 8, 31));

        Assert.Equal(100m, Assert.Single(report.Rows).Balance);
    }

    [Fact]
    public async Task ActivityReport_CalculatesOpeningIncreasesDecreasesAndClosing()
    {
        var fund = new Fund(Guid.NewGuid(), "MISSIONS", "Missions Fund", FundRestriction.DonorRestricted);
        var store = new ReportingStore(fund);
        store.AddActivity(fund.Id, new DateOnly(2026, 7, 31), 100m);
        store.AddActivity(fund.Id, new DateOnly(2026, 8, 5), 75m);
        store.AddActivity(fund.Id, new DateOnly(2026, 8, 10), -20m);

        var report = await new FundReportingService(store).BuildActivityReportAsync(
            fund.Id,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        Assert.Equal(100m, report.OpeningBalance);
        Assert.Equal(75m, report.Increases);
        Assert.Equal(20m, report.Decreases);
        Assert.Equal(155m, report.ClosingBalance);
        Assert.Equal(2, report.Lines.Count);
    }

    [Fact]
    public async Task ActivityReport_RejectsStartDateAfterEndDate()
    {
        var fund = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");
        var service = new FundReportingService(new ReportingStore(fund));

        await Assert.ThrowsAsync<ArgumentException>(() => service.BuildActivityReportAsync(
            fund.Id,
            new DateOnly(2026, 8, 31),
            new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public void RestrictionReview_UnrestrictedFundIsNotApplicable()
    {
        var fund = new Fund(Guid.NewGuid(), "GENERAL", "General Fund");

        var review = new RestrictionReleaseReviewService().Review(fund, 500m, 100m, "Purpose completed");

        Assert.Equal(RestrictionReleaseReviewStatus.NotApplicable, review.Status);
        Assert.False(review.PostsJournal);
    }

    [Fact]
    public void RestrictionReview_RejectsNonPositiveAmount()
    {
        var fund = RestrictedFund();

        var review = new RestrictionReleaseReviewService().Review(fund, 500m, 0m, "Purpose completed");

        Assert.Equal(RestrictionReleaseReviewStatus.InvalidAmount, review.Status);
    }

    [Fact]
    public void RestrictionReview_RejectsAmountAboveAvailableBalance()
    {
        var fund = RestrictedFund();

        var review = new RestrictionReleaseReviewService().Review(fund, 100m, 150m, "Purpose completed");

        Assert.Equal(RestrictionReleaseReviewStatus.InsufficientFundBalance, review.Status);
    }

    [Fact]
    public void RestrictionReview_RequiresEvidenceBeforeAccountantReview()
    {
        var fund = RestrictedFund();

        var review = new RestrictionReleaseReviewService().Review(fund, 500m, 100m, string.Empty);

        Assert.Equal(RestrictionReleaseReviewStatus.EvidenceRequired, review.Status);
    }

    [Fact]
    public void RestrictionReview_EndowmentAlwaysRequiresSpecialReview()
    {
        var fund = new Fund(Guid.NewGuid(), "ENDOW", "Endowment", FundRestriction.Endowment);

        var review = new RestrictionReleaseReviewService().Review(fund, 500m, 100m, "Board spending-policy documentation");

        Assert.Equal(RestrictionReleaseReviewStatus.EndowmentSpecialReviewRequired, review.Status);
        Assert.False(review.PostsJournal);
    }

    [Fact]
    public void RestrictionReview_ValidDonorRestrictedRequestStopsAtAccountantReview()
    {
        var fund = RestrictedFund();

        var review = new RestrictionReleaseReviewService().Review(fund, 500m, 100m, "Mission trip invoice satisfied the donor purpose.");

        Assert.Equal(RestrictionReleaseReviewStatus.AccountantReviewRequired, review.Status);
        Assert.True(review.IsReadyForReview);
        Assert.False(review.PostsJournal);
    }

    private static Fund RestrictedFund() => new(
        Guid.NewGuid(),
        "MISSIONS",
        "Missions Fund",
        FundRestriction.DonorRestricted,
        purpose: "Mission support");

    private sealed class ReportingStore : IFundAccountingStore
    {
        private readonly List<Fund> _funds;
        private readonly List<FundActivityLine> _activity = new();

        public ReportingStore(params Fund[] funds) => _funds = funds.ToList();

        public void AddActivity(Guid fundId, DateOnly date, decimal impact)
        {
            var debit = impact > 0m ? impact : 0m;
            var credit = impact < 0m ? -impact : 0m;
            _activity.Add(new FundActivityLine(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "TEST",
                date,
                fundId,
                Guid.NewGuid(),
                AccountType.Asset,
                debit,
                credit,
                "Test activity",
                string.Empty,
                string.Empty));
        }

        public Task AddFundAsync(Fund fund, CancellationToken cancellationToken = default)
        {
            _funds.Add(fund);
            return Task.CompletedTask;
        }

        public Task UpdateFundAsync(Fund fund, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Fund?> GetFundAsync(Guid fundId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_funds.FirstOrDefault(fund => fund.Id == fundId));

        public Task<IReadOnlyList<Fund>> GetAllFundsAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Fund>>(_funds.Where(fund => includeArchived || fund.Status == FundStatus.Active).ToArray());

        public Task<IReadOnlyDictionary<Guid, Fund>> GetFundsAsync(IEnumerable<Guid> fundIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Fund>>(_funds.Where(fund => fundIds.Contains(fund.Id)).ToDictionary(fund => fund.Id));

        public Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsync(IEnumerable<Guid> fundIds, CancellationToken cancellationToken = default) =>
            GetBalancesAsync(fundIds, asOfDate: null);

        public Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsOfAsync(IEnumerable<Guid> fundIds, DateOnly asOfDate, CancellationToken cancellationToken = default) =>
            GetBalancesAsync(fundIds, asOfDate);

        public Task SavePostedFundJournalAsync(PostedFundJournalEntry postedEntry, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FundActivityLine>> GetFundActivityAsync(
            Guid fundId,
            DateOnly? fromDate = null,
            DateOnly? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var results = _activity
                .Where(line => line.FundId == fundId)
                .Where(line => !fromDate.HasValue || line.PostingDate >= fromDate.Value)
                .Where(line => !toDate.HasValue || line.PostingDate <= toDate.Value)
                .OrderBy(line => line.PostingDate)
                .ToArray();
            return Task.FromResult<IReadOnlyList<FundActivityLine>>(results);
        }

        private Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(IEnumerable<Guid> fundIds, DateOnly? asOfDate)
        {
            var ids = fundIds.Distinct().ToArray();
            var result = ids.ToDictionary(
                id => id,
                id => _activity
                    .Where(line => line.FundId == id)
                    .Where(line => !asOfDate.HasValue || line.PostingDate <= asOfDate.Value)
                    .Sum(line => line.NetAssetImpact));
            return Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(result);
        }
    }
}
