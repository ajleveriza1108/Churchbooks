using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.People;
using ChurchBooks.Accounting.Services;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class PeopleGivingDirectoryTests
{
    [Fact]
    public void PersonProfile_RequiresMemberOrDonorRole()
    {
        Assert.Throws<ArgumentException>(() => new PersonProfile(Guid.NewGuid(), "Ana", "Santos", false, false));
    }

    [Fact]
    public void PersonProfile_ArchiveAndReactivatePreserveIdentity()
    {
        var person = new PersonProfile(Guid.NewGuid(), "Ana", "Santos", true, true, memberNumber: "M-100");
        var archivedAt = new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.FromHours(8));

        var reactivated = person.Archive(archivedAt).Reactivate();

        Assert.Equal(person.Id, reactivated.Id);
        Assert.Equal("M-100", reactivated.MemberNumber);
        Assert.Equal(PersonStatus.Active, reactivated.Status);
        Assert.Null(reactivated.ArchivedUtc);
    }

    [Fact]
    public void Household_DefaultsStatementNameToHouseholdName()
    {
        var household = new Household(Guid.NewGuid(), "Santos Family");

        Assert.Equal("Santos Family", household.StatementName);
    }

    [Fact]
    public void GivingCategory_ArchiveAndReactivatePreserveConfiguration()
    {
        var category = new GivingCategory(Guid.NewGuid(), "TITHE", "Tithes", "Regular tithe", "Core Giving");

        var reactivated = category.Archive(DateTimeOffset.UtcNow).Reactivate();

        Assert.Equal(category.Id, reactivated.Id);
        Assert.Equal("Core Giving", reactivated.GroupName);
        Assert.Equal(GivingCategoryStatus.Active, reactivated.Status);
    }

    [Fact]
    public async Task ManagementService_RejectsDuplicateMemberNumber()
    {
        var store = new MemoryPeopleGivingStore();
        var service = new PeopleGivingManagementService(store);
        await service.SavePersonAsync(new PersonProfile(Guid.NewGuid(), "Ana", "Santos", true, true, memberNumber: "M-100"));

        var ex = await Assert.ThrowsAsync<PeopleGivingManagementException>(() =>
            service.SavePersonAsync(new PersonProfile(Guid.NewGuid(), "Ben", "Reyes", true, false, memberNumber: "m-100")));

        Assert.Contains("already in use", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManagementService_RejectsDuplicateHouseholdName()
    {
        var store = new MemoryPeopleGivingStore();
        var service = new PeopleGivingManagementService(store);
        await service.SaveHouseholdAsync(new Household(Guid.NewGuid(), "Santos Family"));

        await Assert.ThrowsAsync<PeopleGivingManagementException>(() =>
            service.SaveHouseholdAsync(new Household(Guid.NewGuid(), "santos family")));
    }

    [Fact]
    public async Task ManagementService_RejectsDuplicateGivingCategoryCode()
    {
        var store = new MemoryPeopleGivingStore();
        var service = new PeopleGivingManagementService(store);
        await service.SaveGivingCategoryAsync(new GivingCategory(Guid.NewGuid(), "MISSIONS", "Mission Giving"));

        await Assert.ThrowsAsync<PeopleGivingManagementException>(() =>
            service.SaveGivingCategoryAsync(new GivingCategory(Guid.NewGuid(), "missions", "World Missions")));
    }

    [Fact]
    public async Task ManagementService_RejectsDuplicateGivingCategoryName()
    {
        var store = new MemoryPeopleGivingStore();
        var service = new PeopleGivingManagementService(store);
        await service.SaveGivingCategoryAsync(new GivingCategory(Guid.NewGuid(), "LOVE", "Love Offering"));

        await Assert.ThrowsAsync<PeopleGivingManagementException>(() =>
            service.SaveGivingCategoryAsync(new GivingCategory(Guid.NewGuid(), "SPECIAL", "love offering")));
    }

    [Fact]
    public async Task ManagementService_RejectsArchivedHouseholdAssignment()
    {
        var store = new MemoryPeopleGivingStore();
        var service = new PeopleGivingManagementService(store);
        var household = new Household(Guid.NewGuid(), "Archived Family").Archive(DateTimeOffset.UtcNow);
        await store.AddHouseholdAsync(household);

        await Assert.ThrowsAsync<PeopleGivingManagementException>(() =>
            service.SavePersonAsync(new PersonProfile(Guid.NewGuid(), "Cara", "Lopez", false, true, householdId: household.Id)));
    }

    private sealed class MemoryPeopleGivingStore : IPeopleGivingStore
    {
        private readonly Dictionary<Guid, PersonProfile> _people = new();
        private readonly Dictionary<Guid, Household> _households = new();
        private readonly Dictionary<Guid, GivingCategory> _categories = new();

        public Task AddPersonAsync(PersonProfile person, CancellationToken cancellationToken = default) { _people.Add(person.Id, person); return Task.CompletedTask; }
        public Task UpdatePersonAsync(PersonProfile person, CancellationToken cancellationToken = default) { _people[person.Id] = person; return Task.CompletedTask; }
        public Task DeletePersonAsync(Guid personId, CancellationToken cancellationToken = default) { if (!_people.Remove(personId)) throw new InvalidOperationException("The selected person no longer exists."); return Task.CompletedTask; }
        public Task<PersonProfile?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default) => Task.FromResult(_people.GetValueOrDefault(personId));
        public Task<IReadOnlyList<PersonProfile>> GetPeopleAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PersonProfile>>(_people.Values.Where(person => includeArchived || person.Status == PersonStatus.Active).ToArray());
        public Task AddHouseholdAsync(Household household, CancellationToken cancellationToken = default) { _households.Add(household.Id, household); return Task.CompletedTask; }
        public Task UpdateHouseholdAsync(Household household, CancellationToken cancellationToken = default) { _households[household.Id] = household; return Task.CompletedTask; }
        public Task<Household?> GetHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default) => Task.FromResult(_households.GetValueOrDefault(householdId));
        public Task<IReadOnlyList<Household>> GetHouseholdsAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Household>>(_households.Values.Where(household => includeArchived || household.Status == HouseholdStatus.Active).ToArray());
        public Task AddGivingCategoryAsync(GivingCategory category, CancellationToken cancellationToken = default) { _categories.Add(category.Id, category); return Task.CompletedTask; }
        public Task UpdateGivingCategoryAsync(GivingCategory category, CancellationToken cancellationToken = default) { _categories[category.Id] = category; return Task.CompletedTask; }
        public Task<GivingCategory?> GetGivingCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => Task.FromResult(_categories.GetValueOrDefault(categoryId));
        public Task<IReadOnlyList<GivingCategory>> GetGivingCategoriesAsync(bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GivingCategory>>(_categories.Values.Where(category => includeArchived || category.Status == GivingCategoryStatus.Active).ToArray());
    }
}
