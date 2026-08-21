namespace ChurchBooks.Accounting.Setup;

public sealed record CustomSearchAlias
{
    public Guid Id { get; }
    public string AreaKey { get; }
    public string AliasText { get; }

    public CustomSearchAlias(Guid id, string areaKey, string aliasText)
    {
        if (id == Guid.Empty) throw new ArgumentException("Alias ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(areaKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasText);
        Id = id;
        AreaKey = areaKey.Trim();
        AliasText = aliasText.Trim();
    }
}
