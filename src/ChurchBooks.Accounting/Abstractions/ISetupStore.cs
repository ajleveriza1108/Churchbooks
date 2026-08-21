using ChurchBooks.Accounting.Setup;

namespace ChurchBooks.Accounting.Abstractions;

public interface ISetupStore
{
    Task<OrganizationProfile?> GetOrganizationProfileAsync(CancellationToken cancellationToken = default);
    Task SaveOrganizationProfileAsync(OrganizationProfile profile, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TerminologyDefinition>> GetTerminologyOverridesAsync(CancellationToken cancellationToken = default);
    Task SaveTerminologyAsync(TerminologyDefinition definition, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomSearchAlias>> GetCustomSearchAliasesAsync(CancellationToken cancellationToken = default);
    Task AddCustomSearchAliasAsync(CustomSearchAlias alias, CancellationToken cancellationToken = default);
    Task DeleteCustomSearchAliasAsync(Guid aliasId, CancellationToken cancellationToken = default);
}
