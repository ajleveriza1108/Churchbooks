using ChurchBooks.Accounting.Funds;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundDomainTests
{
    [Theory]
    [InlineData(FundRestriction.Unrestricted, FundOverspendPolicy.Allow)]
    [InlineData(FundRestriction.BoardDesignated, FundOverspendPolicy.Warn)]
    [InlineData(FundRestriction.DonorRestricted, FundOverspendPolicy.Block)]
    [InlineData(FundRestriction.Endowment, FundOverspendPolicy.Block)]
    public void Fund_DefaultOverspendPolicy_FollowsRestrictionClass(FundRestriction restriction, FundOverspendPolicy expected)
    {
        var fund = new Fund(Guid.NewGuid(), "F001", "Test Fund", restriction);

        Assert.Equal(expected, fund.OverspendPolicy);
    }

    [Fact]
    public void Fund_ArchiveAndReactivate_PreserveIdentityAndPurpose()
    {
        var original = new Fund(Guid.NewGuid(), "MISSIONS", "Missions", FundRestriction.DonorRestricted, purpose: "Mission support");
        var archivedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.FromHours(8));

        var archived = original.Archive(archivedAt);
        var reactivated = archived.Reactivate();

        Assert.Equal(FundStatus.Archived, archived.Status);
        Assert.Equal(archivedAt, archived.ArchivedUtc);
        Assert.Equal(FundStatus.Active, reactivated.Status);
        Assert.Null(reactivated.ArchivedUtc);
        Assert.Equal(original.Id, reactivated.Id);
        Assert.Equal("Mission support", reactivated.Purpose);
    }
}
