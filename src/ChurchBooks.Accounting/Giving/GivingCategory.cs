namespace ChurchBooks.Accounting.Giving;

public sealed record GivingCategory
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string Description { get; }
    public string GroupName { get; }
    public GivingCategoryStatus Status { get; }
    public DateTimeOffset? ArchivedUtc { get; }

    public GivingCategory(
        Guid id,
        string code,
        string name,
        string? description = null,
        string? groupName = null,
        GivingCategoryStatus status = GivingCategoryStatus.Active,
        DateTimeOffset? archivedUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Giving category ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (status == GivingCategoryStatus.Active && archivedUtc.HasValue)
        {
            throw new ArgumentException("An active giving category cannot have an archived timestamp.", nameof(archivedUtc));
        }

        if (status == GivingCategoryStatus.Archived && !archivedUtc.HasValue)
        {
            throw new ArgumentException("An archived giving category requires an archived timestamp.", nameof(archivedUtc));
        }

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        GroupName = groupName?.Trim() ?? string.Empty;
        Status = status;
        ArchivedUtc = archivedUtc;
    }

    public GivingCategory Archive(DateTimeOffset archivedUtc) => new(Id, Code, Name, Description, GroupName, GivingCategoryStatus.Archived, archivedUtc);

    public GivingCategory Reactivate() => new(Id, Code, Name, Description, GroupName, GivingCategoryStatus.Active, archivedUtc: null);
}
