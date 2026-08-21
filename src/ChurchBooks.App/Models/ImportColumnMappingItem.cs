using CommunityToolkit.Mvvm.ComponentModel;
using ChurchBooks.Accounting.Importing;

namespace ChurchBooks.App.Models;

public sealed partial class ImportColumnMappingItem : ObservableObject
{
    public int ColumnIndex { get; init; }
    public string Header { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public string Samples { get; init; } = string.Empty;

    [ObservableProperty]
    private ImportColumnRole _role;

    public ImportColumnMapping ToDomain() => new(ColumnIndex, Header, Role);
}
