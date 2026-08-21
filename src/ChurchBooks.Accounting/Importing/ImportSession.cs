namespace ChurchBooks.Accounting.Importing;

public sealed class ImportSession
{
    public Guid Id { get; }
    public string FileName { get; }
    public string FileHash { get; }
    public string WorksheetName { get; }
    public ImportSourceKind SourceKind { get; }
    public int SourceRowCount { get; }
    public IReadOnlyList<ImportStagedRow> Rows { get; }

    public ImportSession(Guid id, string fileName, string fileHash, string worksheetName, ImportSourceKind sourceKind, int sourceRowCount, IEnumerable<ImportStagedRow> rows)
    {
        if (id == Guid.Empty) throw new ArgumentException("Import session ID is required.", nameof(id));
        Id = id;
        FileName = Require(fileName, nameof(fileName));
        FileHash = Require(fileHash, nameof(fileHash));
        WorksheetName = (worksheetName ?? string.Empty).Trim();
        SourceKind = sourceKind;
        if (sourceRowCount < 0) throw new ArgumentOutOfRangeException(nameof(sourceRowCount));
        SourceRowCount = sourceRowCount;
        Rows = (rows ?? throw new ArgumentNullException(nameof(rows))).OrderBy(x => x.SourceRowNumber).ToArray();
        if (Rows.Select(x => x.SourceRowNumber).Distinct().Count() != Rows.Count) throw new ArgumentException("Source row numbers must be unique.", nameof(rows));
    }

    public int DuplicateCount => Rows.Count(x => x.IsPotentialDuplicate);
    private static string Require(string value, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A value is required.", parameterName);
        return normalized;
    }
}
