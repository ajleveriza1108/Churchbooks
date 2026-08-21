namespace ChurchBooks.Accounting.Setup;

public sealed record TerminologyDefinition
{
    public string Key { get; }
    public string Singular { get; }
    public string Plural { get; }

    public TerminologyDefinition(string key, string singular, string plural)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(singular);
        ArgumentException.ThrowIfNullOrWhiteSpace(plural);
        Key = key.Trim();
        Singular = singular.Trim();
        Plural = plural.Trim();
    }
}
