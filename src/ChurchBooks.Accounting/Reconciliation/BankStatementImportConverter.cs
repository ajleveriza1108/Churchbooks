using System.Globalization;
using System.Text.Json;
using ChurchBooks.Accounting.Importing;

namespace ChurchBooks.Accounting.Reconciliation;

public sealed class BankStatementImportConverter
{
    public IReadOnlyList<BankStatementLine> Convert(
        ImportSession session,
        Guid bankAccountId,
        BankStatementAmountConvention amountConvention,
        bool includePotentialDuplicates = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (bankAccountId == Guid.Empty) throw new ArgumentException("Bank account ID is required.", nameof(bankAccountId));

        var result = new List<BankStatementLine>();
        foreach (var row in session.Rows)
        {
            if (row.IsPotentialDuplicate && !includePotentialDuplicates) continue;
            using var document = JsonDocument.Parse(row.NormalizedJson);
            var root = document.RootElement;

            var date = ParseDate(GetText(root, "Date"))
                ?? throw new InvalidOperationException($"Row {row.SourceRowNumber} does not contain a valid mapped Date.");

            var amount = ResolveAmount(root, amountConvention);
            if (amount == 0m) continue;

            result.Add(new BankStatementLine(
                Guid.NewGuid(),
                bankAccountId,
                session.Id,
                date,
                amount,
                GetText(root, "Description"),
                GetText(root, "Reference"),
                row.Fingerprint,
                row.SourceRowNumber));
        }

        return result;
    }

    private static decimal ResolveAmount(JsonElement root, BankStatementAmountConvention convention)
    {
        var amount = ParseMoney(GetText(root, "Amount"));
        if (amount.HasValue) return amount.Value;

        if (convention == BankStatementAmountConvention.SignedAmount)
        {
            throw new InvalidOperationException(
                "Signed Amount convention requires a mapped Amount column.");
        }

        var debit = ParseMoney(GetText(root, "Debit")) ?? 0m;
        var credit = ParseMoney(GetText(root, "Credit")) ?? 0m;
        return convention switch
        {
            BankStatementAmountConvention.DebitIncreasesBalance => debit - credit,
            BankStatementAmountConvention.CreditIncreasesBalance => credit - debit,
            _ => throw new ArgumentOutOfRangeException(nameof(convention))
        };
    }

    private static string GetText(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(propertyName, out var value)
            ? value.ToString().Trim()
            : string.Empty;

    private static DateOnly? ParseDate(string text)
    {
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)) return date;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTime)) return DateOnly.FromDateTime(dateTime);
        return null;
    }

    private static decimal? ParseMoney(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        if (normalized.StartsWith('(') && normalized.EndsWith(')'))
        {
            normalized = "-" + normalized[1..^1];
        }
        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }
}
