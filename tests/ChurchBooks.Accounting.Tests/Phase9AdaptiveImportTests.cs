using ChurchBooks.Accounting.Importing;
using ChurchBooks.Accounting.People;
using Xunit;

namespace ChurchBooks.Accounting.Tests;

public sealed class Phase9AdaptiveImportTests
{
    [Fact]
    public void StandardTemplates_ProvideFourClearStartingPoints() =>
        Assert.Equal(4, ChurchBooksStandardImportTemplates.All.Count);

    [Fact]
    public void PeopleTemplate_ContainsStableIdentityColumns()
    {
        var template = ChurchBooksStandardImportTemplates.All.Single(x => x.Purpose == ImportPurpose.PeopleDirectory);
        Assert.Contains("External Person ID", template.Headers);
        Assert.Contains("Member Number", template.Headers);
        Assert.Contains("First Name", template.Headers);
        Assert.Contains("Last Name", template.Headers);
    }

    [Fact]
    public void AdaptiveAnalyzer_DetectsPeopleDirectory()
    {
        var result = Analyze(["Given Name", "Surname", "Email", "Envelope No"], [new[] { "Ana", "Cruz", "ana@example.test", "E1" }]);
        Assert.Equal(ImportPurpose.PeopleDirectory, result.Purpose.Purpose);
    }

    [Fact]
    public void AdaptiveAnalyzer_DetectsGiving()
    {
        var result = Analyze(["Gift Date", "Donor ID", "Contribution Type", "Fund", "Gift Amount"], [new[] { "2026-08-20", "D1", "Tithe", "GEN", "100" }]);
        Assert.Equal(ImportPurpose.Giving, result.Purpose.Purpose);
    }

    [Fact]
    public void AdaptiveAnalyzer_DetectsBankStatement()
    {
        var result = Analyze(["Posting Date", "Description", "Debit", "Credit"], [new[] { "2026-08-20", "Deposit", "", "100" }]);
        Assert.Equal(ImportPurpose.BankStatement, result.Purpose.Purpose);
    }

    [Fact]
    public void AdaptiveAnalyzer_DetectsGeneralLedger()
    {
        var result = Analyze(["Date", "GL Account", "Debit", "Credit", "Reference"], [new[] { "2026-08-20", "1010", "100", "", "R1" }]);
        Assert.Equal(ImportPurpose.GeneralLedger, result.Purpose.Purpose);
    }

    [Fact]
    public void AdaptiveAnalyzer_LeavesUnfamiliarLayoutForReview()
    {
        var result = Analyze(["Alpha", "Beta"], [new[] { "x", "y" }]);
        Assert.Equal(ImportPurpose.Unknown, result.Purpose.Purpose);
    }

    [Fact]
    public void Analyzer_RecognizesCommonPeopleAliases()
    {
        var result = new SmartImportAnalyzer().Analyze(["Given Name", "Surname", "Mobile", "Envelope No"], [new[] { "Ana", "Cruz", "09171234567", "E1" }]);
        Assert.Equal(ImportColumnRole.FirstName, result.Columns[0].SuggestedRole);
        Assert.Equal(ImportColumnRole.LastName, result.Columns[1].SuggestedRole);
        Assert.Equal(ImportColumnRole.Phone, result.Columns[2].SuggestedRole);
        Assert.Equal(ImportColumnRole.MemberNumber, result.Columns[3].SuggestedRole);
    }

    [Fact]
    public void CandidateFactory_ParsesLastCommaFirstName()
    {
        var candidate = new PersonImportCandidateFactory().Create(2, ["Cruz, Ana Marie"], [new ImportColumnMapping(0, "Name", ImportColumnRole.PersonName)], PersonImportDefaultRole.Donor);
        Assert.Equal("Ana", candidate.FirstName);
        Assert.Equal("Marie", candidate.MiddleName);
        Assert.Equal("Cruz", candidate.LastName);
    }

    [Fact]
    public void CandidateFactory_AppliesExplicitSourceDefaultRole()
    {
        var candidate = new PersonImportCandidateFactory().Create(
            2,
            ["Ana", "Cruz"],
            [
                new ImportColumnMapping(0, "First", ImportColumnRole.FirstName),
                new ImportColumnMapping(1, "Last", ImportColumnRole.LastName)
            ],
            PersonImportDefaultRole.Member);
        Assert.True(candidate.IsMember);
        Assert.False(candidate.IsDonor);
    }

    [Fact]
    public void IdentityResolver_MatchesMemberNumberExactly()
    {
        var person = Person("Ana", "Cruz", memberNumber: "E-100");
        var candidate = Candidate("Ana", "Cruz", memberNumber: "E-100");
        var result = new PersonIdentityResolver().Resolve(candidate, [person]);
        Assert.Equal(PersonImportResolutionStatus.ExistingExact, result.Status);
        Assert.Equal(person.Id, result.ExistingPerson?.Id);
    }

    [Fact]
    public void IdentityResolver_NormalizesCountryCodePhoneSafely()
    {
        var person = Person("Ana", "Cruz", phone: "+63 917 123 4567");
        var candidate = Candidate("Ana", "Cruz", phone: "0917-123-4567");
        var result = new PersonIdentityResolver().Resolve(candidate, [person]);
        Assert.Equal(PersonImportResolutionStatus.ExistingExact, result.Status);
    }

    [Fact]
    public void IdentityResolver_NameOnlyMatchRequiresReview()
    {
        var result = new PersonIdentityResolver().Resolve(Candidate("Ana", "Cruz"), [Person("Ana", "Cruz")]);
        Assert.Equal(PersonImportResolutionStatus.NeedsReview, result.Status);
    }

    [Fact]
    public void IdentityResolver_ConflictingStrongIdentifiersAreAmbiguous()
    {
        var first = Person("Ana", "Cruz", memberNumber: "M1");
        var second = Person("Bea", "Santos", email: "shared@example.test");
        var candidate = Candidate("Ana", "Cruz", memberNumber: "M1", email: "shared@example.test");
        var result = new PersonIdentityResolver().Resolve(candidate, [first, second]);
        Assert.Equal(PersonImportResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.ExistingPerson);
    }

    private static AdaptiveImportAnalysis Analyze(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows) =>
        new AdaptiveImportAnalyzer().Analyze(headers, rows);

    private static PersonProfile Person(string first, string last, string memberNumber = "", string email = "", string phone = "") =>
        new(Guid.NewGuid(), first, last, true, true, email: email, phone: phone, memberNumber: memberNumber);

    private static PersonImportCandidate Candidate(string first, string last, string memberNumber = "", string email = "", string phone = "") =>
        new(2, "", memberNumber, first, "", last, "", email, phone, "", true, true);
}
