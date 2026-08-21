namespace ChurchBooks.Accounting.Importing;

public sealed class ImportSourceProfile
{
    public Guid Id { get; }
    public string Name { get; }
    public ImportPurpose Purpose { get; }
    public string SourceSignature { get; }
    public IReadOnlyList<ImportColumnMapping> Mappings { get; }
    public PersonImportDefaultRole DefaultPersonRole { get; }
    public DateTimeOffset? LastUsedUtc { get; }

    public string DisplayLabel => $"{Name} — {PurposeLabel(Purpose)}";

    public ImportSourceProfile(
        Guid id,
        string name,
        ImportPurpose purpose,
        string sourceSignature,
        IEnumerable<ImportColumnMapping> mappings,
        PersonImportDefaultRole defaultPersonRole = PersonImportDefaultRole.Ask,
        DateTimeOffset? lastUsedUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Source profile ID is required.", nameof(id));
        Id = id;
        Name = Require(name, nameof(name));
        Purpose = purpose;
        SourceSignature = Require(sourceSignature, nameof(sourceSignature));
        Mappings = (mappings ?? throw new ArgumentNullException(nameof(mappings))).OrderBy(x => x.ColumnIndex).ToArray();
        if (Mappings.Count == 0) throw new ArgumentException("At least one mapping is required.", nameof(mappings));
        if (Mappings.Select(x => x.ColumnIndex).Distinct().Count() != Mappings.Count)
            throw new ArgumentException("A source column can be mapped only once.", nameof(mappings));
        var activeRoles = Mappings.Where(x => x.Role != ImportColumnRole.Ignore).Select(x => x.Role).ToArray();
        if (activeRoles.Distinct().Count() != activeRoles.Length)
            throw new ArgumentException("Each active import role can be mapped only once.", nameof(mappings));
        DefaultPersonRole = defaultPersonRole;
        LastUsedUtc = lastUsedUtc;
    }

    private static string PurposeLabel(ImportPurpose purpose) => purpose switch
    {
        ImportPurpose.PeopleDirectory => "People",
        ImportPurpose.Giving => "Giving",
        ImportPurpose.BankStatement => "Bank Statement",
        ImportPurpose.GeneralLedger => "General Ledger",
        _ => "Other"
    };

    private static string Require(string value, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A value is required.", parameterName);
        return normalized;
    }
}
