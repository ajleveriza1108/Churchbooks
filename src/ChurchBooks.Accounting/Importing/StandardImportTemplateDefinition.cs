namespace ChurchBooks.Accounting.Importing;

public sealed record StandardImportTemplateDefinition(
    string Name,
    ImportPurpose Purpose,
    IReadOnlyList<string> Headers,
    string Description);
