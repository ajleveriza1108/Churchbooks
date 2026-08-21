namespace ChurchBooks.Accounting.Importing;

public sealed record ImportPreviewRow(
    int SourceRowNumber,
    IReadOnlyList<string> Cells,
    string Fingerprint,
    bool IsPotentialDuplicate);
