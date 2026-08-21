using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Journals;

namespace ChurchBooks.Accounting.Services;

public sealed class FundAccountingEngine
{
    private readonly IAccountingStore _accountingStore;
    private readonly IFundAccountingStore _fundStore;

    public FundAccountingEngine(IAccountingStore accountingStore, IFundAccountingStore fundStore)
    {
        _accountingStore = accountingStore ?? throw new ArgumentNullException(nameof(accountingStore));
        _fundStore = fundStore ?? throw new ArgumentNullException(nameof(fundStore));
    }

    public async Task<PostedFundJournalEntry> PostJournalAsync(
        FundJournalEntry entry,
        DateTimeOffset? postedUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (await _accountingStore.JournalExistsAsync(entry.Entry.Id, cancellationToken))
        {
            throw new FundPostingException(new[] { "This journal entry has already been posted." });
        }

        var period = await _accountingStore.GetPeriodAsync(entry.Entry.PeriodId, cancellationToken)
            ?? throw new FundPostingException(new[] { "The accounting period does not exist." });

        var accountIds = entry.Entry.Lines.Select(static line => line.AccountId).Distinct().ToArray();
        var accounts = await _accountingStore.GetAccountsAsync(accountIds, cancellationToken);
        JournalEntryValidator.ValidateForPosting(entry.Entry, period, accounts);

        var fundIds = entry.FundIds.ToArray();
        var funds = await _fundStore.GetFundsAsync(fundIds, cancellationToken);
        var balances = await _fundStore.GetFundBalancesAsync(fundIds, cancellationToken);
        var assessment = FundJournalValidator.AssessForPosting(entry, funds, accounts, balances);
        if (!assessment.CanPost)
        {
            throw new FundPostingException(assessment.Errors);
        }

        var posted = new PostedFundJournalEntry(entry, postedUtc ?? DateTimeOffset.UtcNow, assessment.Warnings);
        await _fundStore.SavePostedFundJournalAsync(posted, cancellationToken);
        return posted;
    }
}
