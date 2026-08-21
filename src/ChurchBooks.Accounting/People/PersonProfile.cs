namespace ChurchBooks.Accounting.People;

public sealed record PersonProfile
{
    public Guid Id { get; }
    public string FirstName { get; }
    public string MiddleName { get; }
    public string LastName { get; }
    public string PreferredName { get; }
    public string Email { get; }
    public string Phone { get; }
    public string MemberNumber { get; }
    public bool IsMember { get; }
    public bool IsDonor { get; }
    public Guid? HouseholdId { get; }
    public PersonStatus Status { get; }
    public DateTimeOffset? ArchivedUtc { get; }

    public string DisplayName
    {
        get
        {
            var given = string.IsNullOrWhiteSpace(PreferredName) ? FirstName : PreferredName;
            return (given + " " + LastName).Trim();
        }
    }

    public PersonProfile(
        Guid id,
        string firstName,
        string lastName,
        bool isMember,
        bool isDonor,
        string? middleName = null,
        string? preferredName = null,
        string? email = null,
        string? phone = null,
        string? memberNumber = null,
        Guid? householdId = null,
        PersonStatus status = PersonStatus.Active,
        DateTimeOffset? archivedUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Person ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        if (!isMember && !isDonor)
        {
            throw new ArgumentException("A person must be marked as a member, donor, or both.");
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household ID cannot be empty when supplied.", nameof(householdId));
        }

        if (status == PersonStatus.Active && archivedUtc.HasValue)
        {
            throw new ArgumentException("An active person cannot have an archived timestamp.", nameof(archivedUtc));
        }

        if (status == PersonStatus.Archived && !archivedUtc.HasValue)
        {
            throw new ArgumentException("An archived person requires an archived timestamp.", nameof(archivedUtc));
        }

        Id = id;
        FirstName = firstName.Trim();
        MiddleName = middleName?.Trim() ?? string.Empty;
        LastName = lastName.Trim();
        PreferredName = preferredName?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        MemberNumber = memberNumber?.Trim() ?? string.Empty;
        IsMember = isMember;
        IsDonor = isDonor;
        HouseholdId = householdId;
        Status = status;
        ArchivedUtc = archivedUtc;
    }

    public PersonProfile Archive(DateTimeOffset archivedUtc) => new(
        Id, FirstName, LastName, IsMember, IsDonor, MiddleName, PreferredName, Email, Phone, MemberNumber, HouseholdId, PersonStatus.Archived, archivedUtc);

    public PersonProfile Reactivate() => new(
        Id, FirstName, LastName, IsMember, IsDonor, MiddleName, PreferredName, Email, Phone, MemberNumber, HouseholdId, PersonStatus.Active, archivedUtc: null);
}
