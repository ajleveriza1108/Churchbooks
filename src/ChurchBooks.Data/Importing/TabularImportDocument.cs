using ChurchBooks.Accounting.Importing;

namespace ChurchBooks.Data.Importing;

public sealed record TabularImportDocument(
    string FileName,
    string FileHash,
    string WorksheetName,
    ImportSourceKind SourceKind,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int HeaderRowNumber = 1);
