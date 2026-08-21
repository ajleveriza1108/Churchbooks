using ChurchBooks.Accounting.Reconciliation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChurchBooks.App.Models;

public sealed partial class StatementLineSelectionItem : ObservableObject
{
    public BankStatementLine Line { get; }

    [ObservableProperty]
    private bool _isSelected;

    public StatementLineSelectionItem(BankStatementLine line) =>
        Line = line ?? throw new ArgumentNullException(nameof(line));

    public string Date => Line.TransactionDate.ToString("yyyy-MM-dd");
    public string Amount => Line.Amount.ToString("N2");
    public string Description => Line.Description;
    public string Reference => Line.Reference;
}
