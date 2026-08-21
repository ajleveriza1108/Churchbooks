using ChurchBooks.Accounting.Reconciliation;

namespace ChurchBooks.App.Models;

public sealed record ReconciliationMatchGroupListItem(ReconciliationMatchGroup Group)
{
    public string Display =>
        $"{Group.StatementLineIds.Count} statement ↔ {Group.JournalEntryIds.Count} book item(s)";
}
