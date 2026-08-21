using ChurchBooks.Accounting.Importing;

namespace ChurchBooks.App.Models;

public sealed record PersonImportReviewItem(
    int SourceRowNumber,
    string ImportedName,
    string MemberNumber,
    string Email,
    string Status,
    string ExistingPerson,
    string Reason,
    PersonImportResolution Resolution);
