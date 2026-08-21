namespace ChurchBooks.Accounting.Importing;

public sealed class ImportMappingTemplate
{
    public Guid Id { get; }
    public string Name { get; }
    public string SourceSignature { get; }
    public IReadOnlyList<ImportColumnMapping> Mappings { get; }

    public ImportMappingTemplate(Guid id, string name, string sourceSignature, IEnumerable<ImportColumnMapping> mappings)
    {
        if (id == Guid.Empty) throw new ArgumentException("Template ID is required.", nameof(id));
        Id = id;
        Name = Require(name, nameof(name));
        SourceSignature = Require(sourceSignature, nameof(sourceSignature));
        Mappings = (mappings ?? throw new ArgumentNullException(nameof(mappings))).OrderBy(x => x.ColumnIndex).ToArray();
        if (Mappings.Count == 0) throw new ArgumentException("At least one mapping is required.", nameof(mappings));
        if (Mappings.Select(x => x.ColumnIndex).Distinct().Count() != Mappings.Count) throw new ArgumentException("A source column can be mapped only once.", nameof(mappings));
        var activeRoles = Mappings.Where(x => x.Role != ImportColumnRole.Ignore).Select(x => x.Role).ToArray();
        if (activeRoles.Distinct().Count() != activeRoles.Length) throw new ArgumentException("Each active import role can be mapped only once.", nameof(mappings));
    }

    private static string Require(string value, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A value is required.", parameterName);
        return normalized;
    }
}
