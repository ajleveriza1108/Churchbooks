using System.Globalization;
using System.IO;
using System.Text;
using ChurchBooks.Accounting.Reporting;

namespace ChurchBooks.App.Services;

public sealed class CsvReportExportService
{
    public string ExportFundBalanceReport(FundBalanceReport report, string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, $"ChurchBooks-Fund-Balances-{report.AsOfDate:yyyy-MM-dd}.csv");
        var lines = new List<string>
        {
            "Fund Code,Fund Name,Restriction,Status,Balance"
        };
        lines.AddRange(report.Rows.Select(row => string.Join(",",
            Escape(row.Code),
            Escape(row.Name),
            Escape(row.Restriction.ToString()),
            Escape(row.Status.ToString()),
            row.Balance.ToString("0.00", CultureInfo.InvariantCulture))));
        lines.Add($",,,Total,{report.TotalBalance.ToString("0.00", CultureInfo.InvariantCulture)}");
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public string ExportFundActivityReport(FundActivityReport report, string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var safeCode = SanitizeFilePart(report.Fund.Code);
        var path = Path.Combine(directoryPath, $"ChurchBooks-Fund-Activity-{safeCode}-{report.FromDate:yyyy-MM-dd}-{report.ToDate:yyyy-MM-dd}.csv");
        var lines = new List<string>
        {
            "Date,Entry Number,Description,Reference,Account Type,Debit,Credit,Fund Balance Impact,Memo",
            string.Join(",", "Opening Balance", "", "", "", "", "", "", report.OpeningBalance.ToString("0.00", CultureInfo.InvariantCulture), "")
        };
        lines.AddRange(report.Lines.Select(line => string.Join(",",
            line.PostingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Escape(line.EntryNumber),
            Escape(line.Description),
            Escape(line.Reference),
            Escape(line.AccountType.ToString()),
            line.Debit.ToString("0.00", CultureInfo.InvariantCulture),
            line.Credit.ToString("0.00", CultureInfo.InvariantCulture),
            line.NetAssetImpact.ToString("0.00", CultureInfo.InvariantCulture),
            Escape(line.Memo))));
        lines.Add(string.Join(",", "Closing Balance", "", "", "", "", "", "", report.ClosingBalance.ToString("0.00", CultureInfo.InvariantCulture), ""));
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public string ExportTrialBalanceReport(TrialBalanceReport report, string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, $"ChurchBooks-Trial-Balance-{report.AsOfDate:yyyy-MM-dd}.csv");
        var lines = new List<string>
        {
            "Account Code,Account Name,Account Type,Debit,Credit"
        };
        lines.AddRange(report.Rows.Select(row => string.Join(",",
            Escape(row.Code),
            Escape(row.Name),
            Escape(row.Type.ToString()),
            row.Debit.ToString("0.00", CultureInfo.InvariantCulture),
            row.Credit.ToString("0.00", CultureInfo.InvariantCulture))));
        lines.Add(string.Join(",", "", "", "Totals", report.TotalDebit.ToString("0.00", CultureInfo.InvariantCulture), report.TotalCredit.ToString("0.00", CultureInfo.InvariantCulture)));
        lines.Add(string.Join(",", "", "", "Difference", report.Difference.ToString("0.00", CultureInfo.InvariantCulture), ""));
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public string ExportIncomeExpenseReport(IncomeExpenseReport report, string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, $"ChurchBooks-Income-Expense-{report.FromDate:yyyy-MM-dd}-{report.ToDate:yyyy-MM-dd}.csv");
        var lines = new List<string>
        {
            "Section,Account Code,Account Name,Amount"
        };
        lines.AddRange(report.IncomeRows.Select(row => string.Join(",",
            "Income",
            Escape(row.Code),
            Escape(row.Name),
            row.Amount.ToString("0.00", CultureInfo.InvariantCulture))));
        lines.Add(string.Join(",", "Income", "", "Total Income", report.TotalIncome.ToString("0.00", CultureInfo.InvariantCulture)));
        lines.AddRange(report.ExpenseRows.Select(row => string.Join(",",
            "Expense",
            Escape(row.Code),
            Escape(row.Name),
            row.Amount.ToString("0.00", CultureInfo.InvariantCulture))));
        lines.Add(string.Join(",", "Expense", "", "Total Expenses", report.TotalExpenses.ToString("0.00", CultureInfo.InvariantCulture)));
        lines.Add(string.Join(",", "Summary", "", "Net Income", report.NetIncome.ToString("0.00", CultureInfo.InvariantCulture)));
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public string ExportGeneralLedgerReport(GeneralLedgerReport report, string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var safeCode = SanitizeFilePart(report.Account.Code);
        var path = Path.Combine(directoryPath, $"ChurchBooks-General-Ledger-{safeCode}-{report.FromDate:yyyy-MM-dd}-{report.ToDate:yyyy-MM-dd}.csv");
        var lines = new List<string>
        {
            "Date,Entry Number,Description,Reference,Debit,Credit,Running Balance,Memo",
            string.Join(",", "Opening Balance", "", "", "", "", "", report.OpeningBalance.ToString("0.00", CultureInfo.InvariantCulture), "")
        };
        lines.AddRange(report.Rows.Select(row => string.Join(",",
            row.PostingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Escape(row.EntryNumber),
            Escape(row.Description),
            Escape(row.Reference),
            row.Debit.ToString("0.00", CultureInfo.InvariantCulture),
            row.Credit.ToString("0.00", CultureInfo.InvariantCulture),
            row.RunningBalance.ToString("0.00", CultureInfo.InvariantCulture),
            Escape(row.Memo))));
        lines.Add(string.Join(",", "Closing Balance", "", "", "", "", "", report.ClosingBalance.ToString("0.00", CultureInfo.InvariantCulture), ""));
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string SanitizeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Fund" : sanitized;
    }
}
