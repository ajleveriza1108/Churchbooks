using ChurchBooks.Accounting.Importing;

namespace ChurchBooks.Accounting.Abstractions;

public interface ISmartImportStore
{
    Task<ImportMappingTemplate?> FindTemplateAsync(string sourceSignature, CancellationToken cancellationToken = default);
    Task SaveTemplateAsync(ImportMappingTemplate template, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportMappingTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(IEnumerable<string> fingerprints, CancellationToken cancellationToken = default);
    Task StageSessionAsync(ImportSession session, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportSession>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<ImportSession?> GetSessionAsync(Guid importSessionId, CancellationToken cancellationToken = default);
}
