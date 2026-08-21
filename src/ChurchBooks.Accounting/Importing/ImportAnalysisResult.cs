namespace ChurchBooks.Accounting.Importing;

public sealed record ImportAnalysisResult(
    IReadOnlyList<ImportColumnProfile> Columns,
    IReadOnlyList<ImportPreviewRow> PreviewRows,
    IReadOnlyList<string> Warnings);
