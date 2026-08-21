using ChurchBooks.Accounting.Reconciliation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChurchBooks.App.Models;

public sealed partial class ReconciliationBookSelectionItem : ObservableObject
{
    public ReconciliationBookItem Item { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ReconciliationBookSelectionItem(ReconciliationBookItem item) =>
        Item = item ?? throw new ArgumentNullException(nameof(item));

    public string Date => Item.PostingDate.ToString("yyyy-MM-dd");
    public string Amount => Item.Amount.ToString("N2");
    public string EntryNumber => Item.EntryNumber;
    public string Description => Item.Description;
}
