namespace ChurchBooks.Accounting.Importing;

public sealed record PersonImportCandidate(
    int SourceRowNumber,
    string ExternalPersonId,
    string MemberNumber,
    string FirstName,
    string MiddleName,
    string LastName,
    string PreferredName,
    string Email,
    string Phone,
    string HouseholdName,
    bool? IsMember,
    bool? IsDonor)
{
    public string DisplayName => string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public bool HasUsableName => !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);
}
