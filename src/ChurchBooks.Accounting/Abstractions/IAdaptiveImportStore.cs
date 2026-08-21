using ChurchBooks.Accounting.Importing;

namespace ChurchBooks.Accounting.Abstractions;

public interface IAdaptiveImportStore
{
    Task<IReadOnlyList<ImportSourceProfile>> GetSourceProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportSourceProfile>> FindSourceProfilesAsync(string sourceSignature, CancellationToken cancellationToken = default);
    Task SaveSourceProfileAsync(ImportSourceProfile profile, CancellationToken cancellationToken = default);
    Task TouchSourceProfileAsync(Guid profileId, DateTimeOffset usedUtc, CancellationToken cancellationToken = default);
    Task<Guid?> FindLinkedPersonIdAsync(Guid profileId, string externalPersonKey, CancellationToken cancellationToken = default);
    Task SavePersonExternalLinkAsync(Guid profileId, string externalPersonKey, Guid personId, CancellationToken cancellationToken = default);
}
