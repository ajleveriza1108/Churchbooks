namespace ChurchBooks.Accounting.People;

public sealed record Household
{
    public Guid Id { get; }
    public string Name { get; }
    public string StatementName { get; }
    public HouseholdStatus Status { get; }
    public DateTimeOffset? ArchivedUtc { get; }

    public Household(
        Guid id,
        string name,
        string? statementName = null,
        HouseholdStatus status = HouseholdStatus.Active,
        DateTimeOffset? archivedUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Household ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (status == HouseholdStatus.Active && archivedUtc.HasValue)
        {
            throw new ArgumentException("An active household cannot have an archived timestamp.", nameof(archivedUtc));
        }

        if (status == HouseholdStatus.Archived && !archivedUtc.HasValue)
        {
            throw new ArgumentException("An archived household requires an archived timestamp.", nameof(archivedUtc));
        }

        Id = id;
        Name = name.Trim();
        StatementName = string.IsNullOrWhiteSpace(statementName) ? Name : statementName.Trim();
        Status = status;
        ArchivedUtc = archivedUtc;
    }

    public Household Archive(DateTimeOffset archivedUtc) => new(Id, Name, StatementName, HouseholdStatus.Archived, archivedUtc);

    public Household Reactivate() => new(Id, Name, StatementName, HouseholdStatus.Active, archivedUtc: null);
}
