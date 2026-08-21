using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Services;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class FundManagementServiceTests
{
    [Fact]
    public async Task CreateFund_RejectsDuplicateCodeCaseInsensitively()
    {
        var existing = NewFund("GENERAL", "General Fund");
        var store = new InMemoryFundStore(existing);
        var service = new FundManagementService(store);

        var exception = await Assert.ThrowsAsync<FundManagementException>(() =>
            service.CreateFundAsync(NewFund("general", "Another Fund")));

        Assert.Contains("code", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task CreateFund_RejectsDuplicateNameCaseInsensitively()
    {
        var existing = NewFund("GENERAL", "General Fund");
        var store = new InMemoryFundStore(existing);
        var service = new FundManagementService(store);

        var exception = await Assert.ThrowsAsync<FundManagementException>(() =>
            service.CreateFundAsync(NewFund("MISSIONS", "general fund")));

        Assert.Contains("name", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task UpdateFund_AllowsExistingFundToKeepItsOwnCodeAndName()
    {
        var existing = NewFund("GENERAL", "General Fund");
        var store = new InMemoryFundStore(existing);
        var service = new FundManagementService(store);
        var updated = new Fund(existing.Id, existing.Code, existing.Name, purpose: "Updated purpose");

        await service.UpdateFundAsync(updated);

        Assert.Equal("Updated purpose", store.Funds.Single().Purpose);
    }

    [Fact]
    public async Task ArchiveFund_PreservesIdentityAndMarksFundArchived()
    {
        var existing = NewFund("MISSIONS", "Missions Fund");
        var store = new InMemoryFundStore(existing);
        var service = new FundManagementService(store);
        var archivedAt = new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);

        var archived = await service.ArchiveFundAsync(existing.Id, archivedAt);

        Assert.Equal(existing.Id, archived.Id);
        Assert.Equal(FundStatus.Archived, archived.Status);
        Assert.Equal(archivedAt, archived.ArchivedUtc);
    }

    [Fact]
    public async Task ReactivateFund_RestoresArchivedFundWithoutChangingIdentity()
    {
        var active = NewFund("BUILDING", "Building Fund");
        var archived = active.Archive(DateTimeOffset.UtcNow.AddDays(-1));
        var store = new InMemoryFundStore(archived);
        var service = new FundManagementService(store);

        var reactivated = await service.ReactivateFundAsync(archived.Id);

        Assert.Equal(archived.Id, reactivated.Id);
        Assert.Equal(FundStatus.Active, reactivated.Status);
        Assert.Null(reactivated.ArchivedUtc);
    }

    private static Fund NewFund(string code, string name) => new(Guid.NewGuid(), code, name);

    private sealed class InMemoryFundStore : IFundAccountingStore
    {
        public InMemoryFundStore(params Fund[] funds) => Funds.AddRange(funds);
        public List<Fund> Funds { get; } = new();

        public Task AddFundAsync(Fund fund, CancellationToken cancellationToken = default)
        {
            Funds.Add(fund);
            return Task.CompletedTask;
        }

        public Task UpdateFundAsync(Fund fund, CancellationToken cancellationToken = default)
        {
            var index = Funds.FindIndex(existing => existing.Id == fund.Id);
            if (index < 0) throw new InvalidOperationException("Fund not found.");
            Funds[index] = fund;
            return Task.CompletedTask;
        }

        public Task<Fund?> GetFundAsync(Guid fundId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Funds.FirstOrDefault(fund => fund.Id == fundId));

        public Task<IReadOnlyList<Fund>> GetAllFundsAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Fund>>(Funds.Where(fund => includeArchived || fund.Status == FundStatus.Active).ToArray());

        public Task<IReadOnlyDictionary<Guid, Fund>> GetFundsAsync(IEnumerable<Guid> fundIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Fund>>(Funds.Where(fund => fundIds.Contains(fund.Id)).ToDictionary(fund => fund.Id));

        public Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsync(IEnumerable<Guid> fundIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(fundIds.Distinct().ToDictionary(id => id, _ => 0m));

        public Task<IReadOnlyDictionary<Guid, decimal>> GetFundBalancesAsOfAsync(IEnumerable<Guid> fundIds, DateOnly asOfDate, CancellationToken cancellationToken = default) =>
            GetFundBalancesAsync(fundIds, cancellationToken);

        public Task SavePostedFundJournalAsync(PostedFundJournalEntry postedEntry, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FundActivityLine>> GetFundActivityAsync(Guid fundId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
