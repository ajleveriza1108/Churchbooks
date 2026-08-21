namespace ChurchBooks.App.Models;

public sealed class OfferingBatchDraft
{
    public DateTime? ServiceDate { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}
