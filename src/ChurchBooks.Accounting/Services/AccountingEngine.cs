using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Journals;

namespace ChurchBooks.Accounting.Services;

public sealed class AccountingEngine
{
    private readonly IAccountingStore _store;

    public AccountingEngine(IAccountingStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<PostedJournalEntry> PostJournalAsync(
        JournalEntry entry,
        DateTimeOffset? postedUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (await _store.JournalExistsAsync(entry.Id, cancellationToken))
        {
            throw new JournalPostingException(new[] { "This journal entry has already been posted." });
        }

        var period = await _store.GetPeriodAsync(entry.PeriodId, cancellationToken)
            ?? throw new JournalPostingException(new[] { "The accounting period does not exist." });

        var accountIds = entry.Lines.Select(static line => line.AccountId).Distinct().ToArray();
        var accounts = await _store.GetAccountsAsync(accountIds, cancellationToken);

        JournalEntryValidator.ValidateForPosting(entry, period, accounts);

        var posted = new PostedJournalEntry(entry, postedUtc ?? DateTimeOffset.UtcNow);
        await _store.SavePostedJournalAsync(posted, cancellationToken);
        return posted;
    }
}
