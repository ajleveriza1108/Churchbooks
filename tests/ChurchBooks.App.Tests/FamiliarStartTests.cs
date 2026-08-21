using System.IO;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.App.Models;
using ChurchBooks.App.Preferences;
using ChurchBooks.App.Services;
using ChurchBooks.App.Validation;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class FamiliarStartTests
{
    private readonly FundDraftValidator _validator = new();

    [Fact]
    public async Task FundDraftValidator_AcceptsNormalChurchFund()
    {
        var result = await _validator.ValidateAsync(new FundDraft
        {
            Code = "MISSIONS",
            Name = "Missions Fund",
            Purpose = "Mission support",
            Restriction = FundRestriction.DonorRestricted,
            OverspendPolicy = FundOverspendPolicy.Block
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task FundDraftValidator_RequiresCode()
    {
        var result = await _validator.ValidateAsync(new FundDraft { Name = "General Fund" });
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FundDraft.Code));
    }

    [Fact]
    public async Task FundDraftValidator_RejectsUnsafeCodeCharacters()
    {
        var result = await _validator.ValidateAsync(new FundDraft { Code = "GENERAL FUND!", Name = "General Fund" });
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FundDraft.Code));
    }

    [Fact]
    public async Task FundDraftValidator_RequiresName()
    {
        var result = await _validator.ValidateAsync(new FundDraft { Code = "GENERAL" });
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(FundDraft.Name));
    }

    [Theory]
    [InlineData("QuickBooks class")]
    [InlineData("Xero tracking category")]
    [InlineData("restricted fund")]
    public void TerminologyAliases_OpenNativeFunds(string term)
    {
        var service = new TerminologyAliasService();
        Assert.True(service.TryResolve(term, out var section));
        Assert.Equal(WorkspaceSection.Funds, section);
    }

    [Fact]
    public void TerminologyAliases_UnknownTermDoesNotPretendToMatch()
    {
        var service = new TerminologyAliasService();
        Assert.False(service.TryResolve("payroll tax", out _));
    }

    [Fact]
    public void UiPreferenceStore_RoundTripsModeAndHelpLevelLocally()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.App.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "preferences.json");
        try
        {
            var store = new UiPreferenceStore(path);
            var expected = new UiPreferences(WorkspaceMode.Accountant, HelpLevel.Guided);
            store.Save(expected);
            Assert.Equal(expected, store.Load());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
