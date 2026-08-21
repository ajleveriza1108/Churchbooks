namespace ChurchBooks.App.Models;

public sealed class PersonDraft
{
    public string FirstName { get; init; } = string.Empty;
    public string MiddleName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string PreferredName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string MemberNumber { get; init; } = string.Empty;
    public bool IsMember { get; init; }
    public bool IsDonor { get; init; }
}
