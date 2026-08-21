namespace ChurchBooks.Accounting.Importing;

public sealed record ImportColumnProfile(
    int ColumnIndex,
    string Header,
    ImportColumnRole SuggestedRole,
    decimal Confidence,
    IReadOnlyList<string> Samples);
