using ChurchBooks.Accounting.Importing;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class Phase7ImportTests
{
    private static readonly string[] Headers = ["Date", "Description", "Amount", "Fund Code"];
    private static readonly IReadOnlyList<IReadOnlyList<string>> Rows =
    [
        new[] { "2026-08-20", "Offering", "1250.00", "GENERAL" }
    ];

    [Fact]
    public void Analyzer_InfersDate() =>
        Assert.Equal(ImportColumnRole.Date, Analyze().Columns[0].SuggestedRole);

    [Fact]
    public void Analyzer_InfersDescription() =>
        Assert.Equal(ImportColumnRole.Description, Analyze().Columns[1].SuggestedRole);

    [Fact]
    public void Analyzer_InfersAmount() =>
        Assert.Equal(ImportColumnRole.Amount, Analyze().Columns[2].SuggestedRole);

    [Fact]
    public void Analyzer_InfersFundCode() =>
        Assert.Equal(ImportColumnRole.FundCode, Analyze().Columns[3].SuggestedRole);

    [Fact]
    public void Analyzer_DoesNotGuessUnknownNumericHeaderAsAmount()
    {
        var analysis = new SmartImportAnalyzer().Analyze(["Value"], [new[] { "123" }]);

        Assert.Equal(ImportColumnRole.Ignore, analysis.Columns[0].SuggestedRole);
    }

    [Fact]
    public void Analyzer_RecognizesDebitAndCredit()
    {
        var analysis = new SmartImportAnalyzer().Analyze(
            ["Debit", "Credit"],
            [new[] { "10", "20" }]);

        Assert.Equal(ImportColumnRole.Debit, analysis.Columns[0].SuggestedRole);
        Assert.Equal(ImportColumnRole.Credit, analysis.Columns[1].SuggestedRole);
    }

    [Fact]
    public void Signature_IsCaseAndWhitespaceStable()
    {
        var first = SmartImportAnalyzer.BuildSourceSignature([" date ", "Amount"]);
        var second = SmartImportAnalyzer.BuildSourceSignature(["DATE", " amount "]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Fingerprint_ChangesWhenSignedAmountChanges()
    {
        var mappings = new[]
        {
            new ImportColumnMapping(0, "Amount", ImportColumnRole.Amount)
        };

        Assert.NotEqual(
            ImportDuplicateFingerprint.Compute(["10"], mappings),
            ImportDuplicateFingerprint.Compute(["-10"], mappings));
    }

    [Fact]
    public void Fingerprint_DistinguishesDebitFromCredit()
    {
        var debit = new[]
        {
            new ImportColumnMapping(0, "Debit", ImportColumnRole.Debit)
        };
        var credit = new[]
        {
            new ImportColumnMapping(0, "Credit", ImportColumnRole.Credit)
        };

        Assert.NotEqual(
            ImportDuplicateFingerprint.Compute(["10"], debit),
            ImportDuplicateFingerprint.Compute(["10"], credit));
    }

    [Fact]
    public void Template_RejectsDuplicateSourceColumn()
    {
        Assert.Throws<ArgumentException>(() =>
            new ImportMappingTemplate(
                Guid.NewGuid(),
                "T",
                "A",
                [
                    new(0, "A", ImportColumnRole.Amount),
                    new(0, "A", ImportColumnRole.Ignore)
                ]));
    }

    [Fact]
    public void Session_RejectsDuplicateSourceRows()
    {
        var rows = new[]
        {
            new ImportStagedRow(Guid.NewGuid(), 2, "a", "{}", false),
            new ImportStagedRow(Guid.NewGuid(), 2, "b", "{}", false)
        };

        Assert.Throws<ArgumentException>(() =>
            new ImportSession(
                Guid.NewGuid(),
                "a.csv",
                "hash",
                string.Empty,
                ImportSourceKind.Csv,
                2,
                rows));
    }

    [Fact]
    public void Analysis_PreviewIsBoundedToFiftyRows()
    {
        var rows = Enumerable
            .Range(0, 75)
            .Select(i => (IReadOnlyList<string>)new[] { "2026-08-20", "x", i.ToString(), "F" })
            .ToArray();

        var analysis = new SmartImportAnalyzer().Analyze(Headers, rows);

        Assert.Equal(50, analysis.PreviewRows.Count);
    }

    private static ImportAnalysisResult Analyze() =>
        new SmartImportAnalyzer().Analyze(Headers, Rows);
}
