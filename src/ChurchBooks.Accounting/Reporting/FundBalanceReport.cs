namespace ChurchBooks.Accounting.Reporting;

public sealed record FundBalanceReport(
    DateOnly AsOfDate,
    IReadOnlyList<FundBalanceReportRow> Rows)
{
    public decimal TotalBalance => Rows.Sum(static row => row.Balance);
    public decimal RestrictedBalance => Rows.Where(static row => row.IsRestricted).Sum(static row => row.Balance);
    public decimal UnrestrictedAndDesignatedBalance => TotalBalance - RestrictedBalance;
}
