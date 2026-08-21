using System.IO;
using ChurchBooks.Accounting.People;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.App.Validation;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class PeopleGivingUxTests
{
    [Fact]
    public void TerminologyAliases_MemberAndDonorTermsOpenPeople()
    {
        var service = new TerminologyAliasService();

        Assert.True(service.TryResolve("donor household", out var section));
        Assert.Equal(WorkspaceSection.People, section);
    }

    [Fact]
    public async Task PersonDraftValidator_RequiresAtLeastOneRole()
    {
        var result = await new PersonDraftValidator().ValidateAsync(new PersonDraft { FirstName = "Ana", LastName = "Santos" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Member, Donor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HouseholdDraftValidator_RequiresName()
    {
        var result = await new HouseholdDraftValidator().ValidateAsync(new HouseholdDraft());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(HouseholdDraft.Name));
    }

    [Fact]
    public async Task GivingCategoryDraftValidator_RejectsUnsafeCodeCharacters()
    {
        var result = await new GivingCategoryDraftValidator().ValidateAsync(new GivingCategoryDraft { Code = "TITHE / CASH", Name = "Tithes" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GivingCategoryDraft.Code));
    }

    [Fact]
    public async Task PeopleWorkspace_SearchFiltersByMemberNumber()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChurchBooks.App.Phase4.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(root, "phase4.db"));
            await new PeopleGivingDatabaseMigrator(database).InitializeAsync();
            var store = new SqlitePeopleGivingStore(database);
            await store.AddPersonAsync(new PersonProfile(Guid.NewGuid(), "Ana", "Santos", true, true, memberNumber: "M-123"));
            await store.AddPersonAsync(new PersonProfile(Guid.NewGuid(), "Ben", "Reyes", false, true, memberNumber: "D-456"));
            var viewModel = new PeopleGivingWorkspaceViewModel();
            await viewModel.InitializeAsync(database);

            viewModel.SearchText = "M-123";

            Assert.Single(viewModel.People);
            Assert.Equal("Ana Santos", viewModel.People[0].DisplayName);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
