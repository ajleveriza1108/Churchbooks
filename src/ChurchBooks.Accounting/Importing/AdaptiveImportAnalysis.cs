namespace ChurchBooks.Accounting.Importing;

public sealed record AdaptiveImportAnalysis(
    ImportAnalysisResult ColumnAnalysis,
    ImportPurposeDetection Purpose,
    string SourceSignature);
