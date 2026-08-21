using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Journals;
using ChurchBooks.Core.Finance;

namespace ChurchBooks.Accounting.Funds;

public static class FundTransferJournalFactory
{
    public static FundJournalEntry Create(
        Guid journalEntryId,
        string entryNumber,
        Guid periodId,
        DateOnly postingDate,
        string description,
        CurrencyCode baseCurrency,
        Fund sourceFund,
        Fund destinationFund,
        Account sourceBankAccount,
        Account destinationBankAccount,
        Account interfundTransferEquityAccount,
        decimal amount,
        string? reference = null)
    {
        ArgumentNullException.ThrowIfNull(sourceFund);
        ArgumentNullException.ThrowIfNull(destinationFund);
        ArgumentNullException.ThrowIfNull(sourceBankAccount);
        ArgumentNullException.ThrowIfNull(destinationBankAccount);
        ArgumentNullException.ThrowIfNull(interfundTransferEquityAccount);

        if (sourceFund.Id == destinationFund.Id)
        {
            throw new ArgumentException("Source and destination funds must be different.", nameof(destinationFund));
        }

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Fund transfer amount must be positive.");
        }

        if (sourceBankAccount.Type != AccountType.Asset || destinationBankAccount.Type != AccountType.Asset)
        {
            throw new ArgumentException("Fund transfers require asset-type source and destination bank accounts.");
        }

        if (interfundTransferEquityAccount.Type != AccountType.Equity)
        {
            throw new ArgumentException("The interfund transfer clearing account must be an equity account.", nameof(interfundTransferEquityAccount));
        }

        var sourceEquityLine = new JournalLine(Guid.NewGuid(), interfundTransferEquityAccount.Id, amount, 0m, "Interfund transfer out");
        var sourceBankLine = new JournalLine(Guid.NewGuid(), sourceBankAccount.Id, 0m, amount, "Fund custody decrease");
        var destinationBankLine = new JournalLine(Guid.NewGuid(), destinationBankAccount.Id, amount, 0m, "Fund custody increase");
        var destinationEquityLine = new JournalLine(Guid.NewGuid(), interfundTransferEquityAccount.Id, 0m, amount, "Interfund transfer in");

        var journal = new JournalEntry(
            journalEntryId,
            entryNumber,
            periodId,
            postingDate,
            description,
            baseCurrency,
            new[]
            {
                sourceEquityLine,
                sourceBankLine,
                destinationBankLine,
                destinationEquityLine
            },
            reference);

        return new FundJournalEntry(
            journal,
            new[]
            {
                new FundAssignment(sourceEquityLine.Id, sourceFund.Id),
                new FundAssignment(sourceBankLine.Id, sourceFund.Id),
                new FundAssignment(destinationBankLine.Id, destinationFund.Id),
                new FundAssignment(destinationEquityLine.Id, destinationFund.Id)
            });
    }
}
