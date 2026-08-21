namespace ChurchBooks.Accounting.Importing;

public sealed record ImportStagedRow
{
    public Guid Id { get; }
    public int SourceRowNumber { get; }
    public string Fingerprint { get; }
    public string NormalizedJson { get; }
    public bool IsPotentialDuplicate { get; }

    public ImportStagedRow(Guid id, int sourceRowNumber, string fingerprint, string normalizedJson, bool isPotentialDuplicate)
    {
        if (id == Guid.Empty) throw new ArgumentException("Staged row ID is required.", nameof(id));
        if (sourceRowNumber < 2) throw new ArgumentOutOfRangeException(nameof(sourceRowNumber));
        var normalizedFingerprint = fingerprint?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFingerprint)) throw new ArgumentException("Fingerprint is required.", nameof(fingerprint));
        Id = id;
        SourceRowNumber = sourceRowNumber;
        Fingerprint = normalizedFingerprint;
        NormalizedJson = normalizedJson ?? "{}";
        IsPotentialDuplicate = isPotentialDuplicate;
    }
}
