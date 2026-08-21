namespace ChurchBooks.Accounting.Expenses;

public sealed record Vendor
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string TaxId { get; }
    public string Email { get; }
    public string Phone { get; }
    public VendorStatus Status { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? ArchivedUtc { get; }

    public Vendor(
        Guid id,
        string code,
        string name,
        string? taxId = null,
        string? email = null,
        string? phone = null,
        VendorStatus status = VendorStatus.Active,
        DateTimeOffset? createdUtc = null,
        DateTimeOffset? archivedUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Vendor ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (status == VendorStatus.Active && archivedUtc.HasValue)
            throw new ArgumentException("An active vendor cannot have an archived timestamp.", nameof(archivedUtc));
        if (status == VendorStatus.Archived && !archivedUtc.HasValue)
            throw new ArgumentException("An archived vendor requires an archived timestamp.", nameof(archivedUtc));

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        TaxId = taxId?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Status = status;
        CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow;
        ArchivedUtc = archivedUtc;
    }

    public Vendor Archive(DateTimeOffset archivedUtc) => new(
        Id, Code, Name, TaxId, Email, Phone, VendorStatus.Archived, CreatedUtc, archivedUtc);

    public Vendor Reactivate() => new(
        Id, Code, Name, TaxId, Email, Phone, VendorStatus.Active, CreatedUtc, archivedUtc: null);
}
