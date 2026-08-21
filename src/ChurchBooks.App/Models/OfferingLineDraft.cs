using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Giving;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChurchBooks.App.Models;

public sealed partial class OfferingLineDraft : ObservableObject
{
    [ObservableProperty] private GivingCategory? _givingCategory;
    [ObservableProperty] private Fund? _fund;
    [ObservableProperty] private string _amountText = string.Empty;
}
