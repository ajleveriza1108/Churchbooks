namespace ChurchBooks.Accounting.Integrity;

public sealed record FundIntegrityReport(
    DateTimeOffset ScannedUtc,
    IReadOnlyList<FundIntegrityFinding> Findings)
{
    public int CriticalCount => Findings.Count(static finding => finding.Severity == FundIntegritySeverity.Critical);
    public int WarningCount => Findings.Count(static finding => finding.Severity == FundIntegritySeverity.Warning);
    public bool HasBlockingIssues => CriticalCount > 0;
    public bool IsClean => Findings.Count == 0;
}
