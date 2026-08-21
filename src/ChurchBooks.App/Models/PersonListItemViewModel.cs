using ChurchBooks.Accounting.People;

namespace ChurchBooks.App.Models;

public sealed record PersonListItemViewModel(
    Guid Id,
    string DisplayName,
    string MemberNumber,
    bool IsMember,
    bool IsDonor,
    string HouseholdName,
    string Email,
    string Phone,
    PersonStatus Status)
{
    public string RoleLabel => IsMember && IsDonor ? "Member + Donor" : IsMember ? "Member" : "Donor";
    public string StatusLabel => Status == PersonStatus.Active ? "Active" : "Archived";
}
