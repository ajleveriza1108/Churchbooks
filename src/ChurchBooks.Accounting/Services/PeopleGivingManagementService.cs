using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.People;

namespace ChurchBooks.Accounting.Services;

public sealed class PeopleGivingManagementService
{
    private readonly IPeopleGivingStore _store;

    public PeopleGivingManagementService(IPeopleGivingStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<IReadOnlyList<PersonProfile>> GetPeopleAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        _store.GetPeopleAsync(includeArchived, cancellationToken);

    public Task<IReadOnlyList<Household>> GetHouseholdsAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        _store.GetHouseholdsAsync(includeArchived, cancellationToken);

    public Task<IReadOnlyList<GivingCategory>> GetGivingCategoriesAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        _store.GetGivingCategoriesAsync(includeArchived, cancellationToken);

    public async Task<PersonProfile> SavePersonAsync(PersonProfile person, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(person);
        await EnsureHouseholdAvailableAsync(person.HouseholdId, cancellationToken);
        await EnsureUniqueMemberNumberAsync(person, cancellationToken);

        var existing = await _store.GetPersonAsync(person.Id, cancellationToken);
        if (existing is null)
        {
            await _store.AddPersonAsync(person, cancellationToken);
        }
        else
        {
            await _store.UpdatePersonAsync(person, cancellationToken);
        }

        return person;
    }

    public async Task<Household> SaveHouseholdAsync(Household household, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(household);
        var households = await _store.GetHouseholdsAsync(includeArchived: true, cancellationToken);
        if (households.Any(existing => existing.Id != household.Id && string.Equals(existing.Name, household.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new PeopleGivingManagementException($"Household name '{household.Name}' is already in use.");
        }

        var existing = await _store.GetHouseholdAsync(household.Id, cancellationToken);
        if (existing is null)
        {
            await _store.AddHouseholdAsync(household, cancellationToken);
        }
        else
        {
            await _store.UpdateHouseholdAsync(household, cancellationToken);
        }

        return household;
    }

    public async Task<GivingCategory> SaveGivingCategoryAsync(GivingCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        var categories = await _store.GetGivingCategoriesAsync(includeArchived: true, cancellationToken);
        if (categories.Any(existing => existing.Id != category.Id && string.Equals(existing.Code, category.Code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new PeopleGivingManagementException($"Giving category code '{category.Code}' is already in use.");
        }

        if (categories.Any(existing => existing.Id != category.Id && string.Equals(existing.Name, category.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new PeopleGivingManagementException($"Giving category name '{category.Name}' is already in use.");
        }

        var existing = await _store.GetGivingCategoryAsync(category.Id, cancellationToken);
        if (existing is null)
        {
            await _store.AddGivingCategoryAsync(category, cancellationToken);
        }
        else
        {
            await _store.UpdateGivingCategoryAsync(category, cancellationToken);
        }

        return category;
    }

    public async Task<PersonProfile> ArchivePersonAsync(Guid personId, DateTimeOffset? archivedUtc = null, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetPersonAsync(personId, cancellationToken)
            ?? throw new PeopleGivingManagementException("The selected person no longer exists.");
        if (current.Status == PersonStatus.Archived)
        {
            return current;
        }

        var archived = current.Archive(archivedUtc ?? DateTimeOffset.UtcNow);
        await _store.UpdatePersonAsync(archived, cancellationToken);
        return archived;
    }

    public async Task<PersonProfile> ReactivatePersonAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetPersonAsync(personId, cancellationToken)
            ?? throw new PeopleGivingManagementException("The selected person no longer exists.");
        var reactivated = current.Status == PersonStatus.Active ? current : current.Reactivate();
        await EnsureHouseholdAvailableAsync(reactivated.HouseholdId, cancellationToken);
        await EnsureUniqueMemberNumberAsync(reactivated, cancellationToken);
        await _store.UpdatePersonAsync(reactivated, cancellationToken);
        return reactivated;
    }

    public async Task<Household> ArchiveHouseholdAsync(Guid householdId, DateTimeOffset? archivedUtc = null, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetHouseholdAsync(householdId, cancellationToken)
            ?? throw new PeopleGivingManagementException("The selected household no longer exists.");
        if (current.Status == HouseholdStatus.Archived)
        {
            return current;
        }

        var people = await _store.GetPeopleAsync(includeArchived: false, cancellationToken);
        if (people.Any(person => person.HouseholdId == householdId))
        {
            throw new PeopleGivingManagementException("Move active people out of this household before archiving it.");
        }

        var archived = current.Archive(archivedUtc ?? DateTimeOffset.UtcNow);
        await _store.UpdateHouseholdAsync(archived, cancellationToken);
        return archived;
    }

    public async Task<Household> ReactivateHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetHouseholdAsync(householdId, cancellationToken)
            ?? throw new PeopleGivingManagementException("The selected household no longer exists.");
        var reactivated = current.Status == HouseholdStatus.Active ? current : current.Reactivate();
        await SaveHouseholdAsync(reactivated, cancellationToken);
        return reactivated;
    }

    public async Task<GivingCategory> ArchiveGivingCategoryAsync(Guid categoryId, DateTimeOffset? archivedUtc = null, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetGivingCategoryAsync(categoryId, cancellationToken)
            ?? throw new PeopleGivingManagementException("The selected giving category no longer exists.");
        if (current.Status == GivingCategoryStatus.Archived)
        {
            return current;
        }

        var archived = current.Archive(archivedUtc ?? DateTimeOffset.UtcNow);
        await _store.UpdateGivingCategoryAsync(archived, cancellationToken);
        return archived;
    }

    public async Task<GivingCategory> ReactivateGivingCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetGivingCategoryAsync(categoryId, cancellationToken)
            ?? throw new PeopleGivingManagementException("The selected giving category no longer exists.");
        var reactivated = current.Status == GivingCategoryStatus.Active ? current : current.Reactivate();
        await SaveGivingCategoryAsync(reactivated, cancellationToken);
        return reactivated;
    }

    private async Task EnsureHouseholdAvailableAsync(Guid? householdId, CancellationToken cancellationToken)
    {
        if (!householdId.HasValue)
        {
            return;
        }

        var household = await _store.GetHouseholdAsync(householdId.Value, cancellationToken);
        if (household is null)
        {
            throw new PeopleGivingManagementException("The selected household does not exist.");
        }

        if (household.Status != HouseholdStatus.Active)
        {
            throw new PeopleGivingManagementException("An archived household cannot be assigned to an active person.");
        }
    }

    private async Task EnsureUniqueMemberNumberAsync(PersonProfile person, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(person.MemberNumber))
        {
            return;
        }

        var people = await _store.GetPeopleAsync(includeArchived: true, cancellationToken);
        if (people.Any(existing => existing.Id != person.Id && string.Equals(existing.MemberNumber, person.MemberNumber, StringComparison.OrdinalIgnoreCase)))
        {
            throw new PeopleGivingManagementException($"Member number '{person.MemberNumber}' is already in use.");
        }
    }
}
