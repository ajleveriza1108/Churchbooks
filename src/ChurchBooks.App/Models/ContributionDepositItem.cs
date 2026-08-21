using CommunityToolkit.Mvvm.ComponentModel;
namespace ChurchBooks.App.Models;
public sealed partial class ContributionDepositItem : ObservableObject
{
    public Guid ContributionId { get; init; }
    public string Display { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string AmountDisplay => $"PHP {Amount:N2}";
    [ObservableProperty] private bool _isSelected;
}
