using System.IO;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.App.Models;
using ChurchBooks.App.Services;
using ChurchBooks.App.Validation;
using ChurchBooks.App.ViewModels;
using ChurchBooks.Data.Storage;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class OfferingUxTests
{
    [Fact]
    public void TerminologyAliases_OfferingTermsOpenGiving()
    {
        var service = new TerminologyAliasService();
        Assert.True(service.TryResolve("weekly offering", out var section));
        Assert.Equal(WorkspaceSection.Giving, section);
    }

    [Fact]
    public void ChartChoices_IncludeSixCommonOfferingCharts()
    {
        var choices = Enum.GetValues<OfferingChartKind>();
        Assert.Equal(6, choices.Length);
        Assert.Contains(OfferingChartKind.Pie, choices);
        Assert.Contains(OfferingChartKind.Line, choices);
        Assert.Contains(OfferingChartKind.Donut, choices);
        Assert.Contains(OfferingChartKind.Area, choices);
    }

    [Fact]
    public async Task BatchValidator_RequiresDateAndName()
    {
        var result = await new OfferingBatchDraftValidator().ValidateAsync(new OfferingBatchDraft());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OfferingBatchDraft.ServiceDate));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(OfferingBatchDraft.Name));
    }

    [Fact]
    public async Task LineValidator_RejectsIncompleteOrNonPositiveRow()
    {
        var result = await new OfferingLineDraftValidator().ValidateAsync(new OfferingLineDraft { AmountText = "0" });
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }

    [Fact]
    public async Task GivingWorkspace_InitializesSchemaFiveAndCommonChartDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChurchBooks.App.Phase5.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = new ChurchBooksDatabase(Path.Combine(directory, "phase5.db"));
            var viewModel = new GivingWorkspaceViewModel();
            await viewModel.InitializeAsync(database);
            Assert.Equal(OfferingChartKind.Line, viewModel.SelectedChartKind);
            Assert.Equal(OfferingPeriodGranularity.Month, viewModel.SelectedGranularity);
            Assert.Single(viewModel.BreakdownLines);
            Assert.Contains(viewModel.GivingCategories, category => category.Code == "TITHE" && category.Name == "Tithes");
        }
        finally
        {
            DeleteDirectoryWithRetry(directory);
        }
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        const int maxAttempts = 20;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!Directory.Exists(directory)) return;
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.Threading.Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.Threading.Thread.Sleep(100);
            }
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
