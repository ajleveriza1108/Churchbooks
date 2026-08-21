using System.IO;
using ChurchBooks.Accounting.ChartOfAccounts;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Reporting;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class FundReportingUxTests
{
    [Fact]
    public void TerminologyAliases_ReportTermsOpenReports()
    {
        var service = new TerminologyAliasService();

        Assert.True(service.TryResolve("statement of activities", out var section));
        Assert.Equal(WorkspaceSection.Reports, section);
    }

    [Fact]
    public void TerminologyAliases_AuditTermsOpenIntegrityCenter()
    {
        var service = new TerminologyAliasService();

        Assert.True(service.TryResolve("audit health check", out var section));
        Assert.Equal(WorkspaceSection.Integrity, section);
    }

    [Fact]
    public void CsvExporter_BalanceReportQuotesTextAndWritesTotal()
    {
        var root = CreateTestRoot();
        try
        {
            var report = new FundBalanceReport(
                new DateOnly(2026, 8, 31),
                new[]
                {
                    new FundBalanceReportRow(Guid.NewGuid(), "GEN", "General, Main", FundRestriction.Unrestricted, FundStatus.Active, 123.45m)
                });

            var path = new CsvReportExportService().ExportFundBalanceReport(report, root);
            var text = File.ReadAllText(path);

            Assert.Contains("\"General, Main\"", text, StringComparison.Ordinal);
            Assert.Contains("123.45", text, StringComparison.Ordinal);
            Assert.Contains("Total", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    [Fact]
    public void CsvExporter_ActivityReportIncludesDateRangeAndBalanceImpact()
    {
        var root = CreateTestRoot();
        try
        {
            var fund = new Fund(Guid.NewGuid(), "MISSIONS", "Missions", FundRestriction.DonorRestricted, purpose: "Mission support");
            var line = new FundActivityLine(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "JE-1",
                new DateOnly(2026, 8, 5),
                fund.Id,
                Guid.NewGuid(),
                AccountType.Asset,
                75m,
                0m,
                "Contribution",
                "REF",
                "Memo");
            var report = new FundActivityReport(
                fund,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                100m,
                75m,
                0m,
                175m,
                new[] { line });

            var path = new CsvReportExportService().ExportFundActivityReport(report, root);
            var text = File.ReadAllText(path);

            Assert.Contains("2026-08-05", text, StringComparison.Ordinal);
            Assert.Contains("75.00", text, StringComparison.Ordinal);
            Assert.Contains("Closing Balance", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfPresent(root);
        }
    }

    private static string CreateTestRoot() => Path.Combine(Path.GetTempPath(), "ChurchBooks.App.Phase3D.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
