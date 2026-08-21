using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.People;

namespace ChurchBooks.Accounting.Abstractions;

public interface IPeopleGivingStore
{
    Task AddPersonAsync(PersonProfile person, CancellationToken cancellationToken = default);
    Task UpdatePersonAsync(PersonProfile person, CancellationToken cancellationToken = default);
    Task DeletePersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<PersonProfile?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PersonProfile>> GetPeopleAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task AddHouseholdAsync(Household household, CancellationToken cancellationToken = default);
    Task UpdateHouseholdAsync(Household household, CancellationToken cancellationToken = default);
    Task<Household?> GetHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Household>> GetHouseholdsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task AddGivingCategoryAsync(GivingCategory category, CancellationToken cancellationToken = default);
    Task UpdateGivingCategoryAsync(GivingCategory category, CancellationToken cancellationToken = default);
    Task<GivingCategory?> GetGivingCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GivingCategory>> GetGivingCategoriesAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
}
