namespace ChurchBooks.Accounting.Importing;

public sealed record ImportColumnMapping
{
    public int ColumnIndex { get; }
    public string Header { get; }
    public ImportColumnRole Role { get; }

    public ImportColumnMapping(int columnIndex, string header, ImportColumnRole role)
    {
        if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        ColumnIndex = columnIndex;
        Header = (header ?? string.Empty).Trim();
        Role = role;
    }
}
