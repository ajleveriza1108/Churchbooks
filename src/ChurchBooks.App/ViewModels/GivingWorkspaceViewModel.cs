using System.Collections.ObjectModel;
using System.Globalization;
using ChurchBooks.Accounting.Funds;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.Accounting.People;
using ChurchBooks.Accounting.Services;
using ChurchBooks.Accounting.Setup;
using ChurchBooks.App.Models;
using ChurchBooks.App.Validation;
using ChurchBooks.Core.Finance;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class GivingWorkspaceViewModel : ObservableObject
{
    private readonly OfferingBatchDraftValidator _batchValidator = new();
    private readonly OfferingLineDraftValidator _lineValidator = new();
    private SqliteOfferingStore? _offeringStore;
    private SqlitePeopleGivingStore? _peopleStore;
    private SqliteFundAccountingStore? _fundStore;
    private OfferingManagementService? _management;
    private OfferingReportingService? _reporting;

    public ObservableCollection<PersonProfile> Donors { get; } = new();
    public ObservableCollection<OfferingBatch> Batches { get; } = new();
    public ObservableCollection<GivingCategory> GivingCategories { get; } = new();
    public ObservableCollection<Fund> Funds { get; } = new();
    public ObservableCollection<OfferingLineDraft> BreakdownLines { get; } = new();
    public ObservableCollection<OfferingChartPoint> ChartPoints { get; } = new();
    public ObservableCollection<OfferingChartPoint> ChurchMonthCategoryPoints { get; } = new();
    public IReadOnlyList<OfferingChartKind> ChartKinds { get; } = Enum.GetValues<OfferingChartKind>();
    public IReadOnlyList<OfferingPeriodGranularity> Granularities { get; } = Enum.GetValues<OfferingPeriodGranularity>();

    [ObservableProperty] private PersonProfile? _selectedPerson;
    [ObservableProperty] private OfferingBatch? _selectedBatch;
    [ObservableProperty] private DateTime? _batchDate = DateTime.Today;
    [ObservableProperty] private string _batchName = "Sunday Service";
    [ObservableProperty] private string _batchReference = string.Empty;
    [ObservableProperty] private string _contributionReference = string.Empty;
    [ObservableProperty] private string _contributionMemo = string.Empty;
    [ObservableProperty] private string _entryError = string.Empty;
    [ObservableProperty] private string _statusMessage = "Select a donor and an open offering batch.";
    [ObservableProperty] private DateTime? _analyticsAsOfDate = DateTime.Today;
    [ObservableProperty] private OfferingPeriodGranularity _selectedGranularity = OfferingPeriodGranularity.Month;
    [ObservableProperty] private OfferingChartKind _selectedChartKind = OfferingChartKind.Line;
    [ObservableProperty] private string _personWeekTotal = "PHP 0.00";
    [ObservableProperty] private string _personMonthTotal = "PHP 0.00";
    [ObservableProperty] private string _personYearTotal = "PHP 0.00";
    [ObservableProperty] private string _personAllTimeTotal = "PHP 0.00";
    [ObservableProperty] private string _churchWeekTotal = "PHP 0.00";
    [ObservableProperty] private string _churchMonthTotal = "PHP 0.00";
    [ObservableProperty] private string _churchYearTotal = "PHP 0.00";
    [ObservableProperty] private string _chartCaption = "Monthly trend for the selected individual.";

    [ObservableProperty] private string _offeringSingularLabel = "Offering";
    [ObservableProperty] private string _offeringPluralLabel = "Offerings";
    [ObservableProperty] private string _serviceSingularLabel = "Service";
    [ObservableProperty] private string _givingCategorySingularLabel = "Giving Category";
    [ObservableProperty] private string _fundSingularLabel = "Fund";
    [ObservableProperty] private string _donorSingularLabel = "Donor";
    [ObservableProperty] private string _baseCurrency = "PHP";

    public decimal DraftTotal => BreakdownLines.Sum(line => ParseAmount(line.AmountText));
    public string DraftTotalDisplay => $"{BaseCurrency} {DraftTotal:N2}";
    public bool UsesBreakdownChart => SelectedChartKind is OfferingChartKind.Pie or OfferingChartKind.Donut;

    public void ApplyTerminology(TerminologyCatalog catalog, string? baseCurrency = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        OfferingSingularLabel = catalog.Singular(TerminologyKeys.Offering);
        OfferingPluralLabel = catalog.Plural(TerminologyKeys.Offering);
        ServiceSingularLabel = catalog.Singular(TerminologyKeys.Service);
        GivingCategorySingularLabel = catalog.Singular(TerminologyKeys.GivingCategory);
        FundSingularLabel = catalog.Singular(TerminologyKeys.Fund);
        DonorSingularLabel = catalog.Singular(TerminologyKeys.Donor);
        if (!string.IsNullOrWhiteSpace(baseCurrency)) BaseCurrency = baseCurrency.Trim().ToUpperInvariant();
        if (string.Equals(BatchName, "Sunday Service", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(BatchName))
            BatchName = "Sunday " + ServiceSingularLabel;
        OnPropertyChanged(nameof(DraftTotalDisplay));
    }

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await new OfferingDatabaseMigrator(database).InitializeAsync(cancellationToken);
        _peopleStore = new SqlitePeopleGivingStore(database);
        _fundStore = new SqliteFundAccountingStore(database);
        _offeringStore = new SqliteOfferingStore(database);
        _management = new OfferingManagementService(_offeringStore, _peopleStore, _fundStore);
        _reporting = new OfferingReportingService(_offeringStore, _peopleStore);
        await RefreshAsync(cancellationToken);
        if (BreakdownLines.Count == 0) AddBreakdownLine();
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_peopleStore is null || _fundStore is null || _offeringStore is null) return;
        var selectedPersonId = SelectedPerson?.Id;
        var selectedBatchId = SelectedBatch?.Id;
        Replace(Donors, (await _peopleStore.GetPeopleAsync(cancellationToken: cancellationToken)).Where(static person => person.IsDonor));
        Replace(GivingCategories, (await _peopleStore.GetGivingCategoriesAsync(cancellationToken: cancellationToken)).Where(static category => category.Status == GivingCategoryStatus.Active));
        Replace(Funds, (await _fundStore.GetAllFundsAsync(cancellationToken: cancellationToken)).Where(static fund => fund.Status == FundStatus.Active));
        Replace(Batches, (await _offeringStore.GetBatchesAsync(cancellationToken: cancellationToken)).Where(static batch => batch.Status == OfferingBatchStatus.Open));
        SelectedPerson = selectedPersonId.HasValue ? Donors.FirstOrDefault(person => person.Id == selectedPersonId.Value) : null;
        SelectedBatch = selectedBatchId.HasValue ? Batches.FirstOrDefault(batch => batch.Id == selectedBatchId.Value) : Batches.FirstOrDefault();
        await RefreshAnalyticsAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task CreateBatchAsync()
    {
        if (_management is null) { EntryError = "Offering service is not ready."; return; }
        var draft = new OfferingBatchDraft { ServiceDate = BatchDate, Name = BatchName.Trim(), Reference = BatchReference.Trim() };
        var validation = await _batchValidator.ValidateAsync(draft);
        if (!validation.IsValid) { EntryError = string.Join(" ", validation.Errors.Select(static error => error.ErrorMessage).Distinct(StringComparer.Ordinal)); return; }
        try
        {
            var batch = new OfferingBatch(Guid.NewGuid(), DateOnly.FromDateTime(draft.ServiceDate!.Value.Date), draft.Name, draft.Reference);
            await _management.CreateBatchAsync(batch);
            EntryError = string.Empty;
            await RefreshAsync();
            SelectedBatch = Batches.FirstOrDefault(item => item.Id == batch.Id);
            StatusMessage = $"Offering batch '{batch.Name}' created for {batch.ServiceDate:yyyy-MM-dd}.";
        }
        catch (Exception ex) when (ex is OfferingManagementException or InvalidOperationException)
        {
            EntryError = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CloseBatchAsync()
    {
        if (_management is null || SelectedBatch is null) return;
        try
        {
            await _management.CloseBatchAsync(SelectedBatch.Id);
            await RefreshAsync();
            StatusMessage = "Offering batch closed. Historical individual contributions remain immutable.";
        }
        catch (OfferingManagementException ex) { EntryError = ex.Message; }
    }

    [RelayCommand]
    private void AddBreakdownLine()
    {
        var line = new OfferingLineDraft { GivingCategory = null, Fund = null, AmountText = string.Empty };
        line.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(DraftTotal)); OnPropertyChanged(nameof(DraftTotalDisplay)); };
        BreakdownLines.Add(line);
        OnPropertyChanged(nameof(DraftTotal));
        OnPropertyChanged(nameof(DraftTotalDisplay));
    }

    [RelayCommand]
    private void RemoveBreakdownLine(OfferingLineDraft? line)
    {
        if (line is null || BreakdownLines.Count <= 1) return;
        BreakdownLines.Remove(line);
        OnPropertyChanged(nameof(DraftTotal));
        OnPropertyChanged(nameof(DraftTotalDisplay));
    }

    [RelayCommand]
    private async Task SaveContributionAsync()
    {
        if (_management is null || SelectedPerson is null || SelectedBatch is null)
        {
            EntryError = "Select an individual donor and an open offering batch.";
            return;
        }
        var validationErrors = new List<string>();
        foreach (var line in BreakdownLines)
        {
            var validation = await _lineValidator.ValidateAsync(line);
            validationErrors.AddRange(validation.Errors.Select(static error => error.ErrorMessage));
        }
        var duplicatePairs = BreakdownLines.Where(static line => line.GivingCategory is not null && line.Fund is not null)
            .GroupBy(static line => (line.GivingCategory!.Id, line.Fund!.Id)).Any(static group => group.Count() > 1);
        if (duplicatePairs) validationErrors.Add("Combine duplicate giving-category and fund rows before saving.");
        if (validationErrors.Count > 0)
        {
            EntryError = string.Join(" ", validationErrors.Distinct(StringComparer.Ordinal));
            return;
        }
        try
        {
            var lines = BreakdownLines.Select(line => new ContributionLine(
                Guid.NewGuid(), line.GivingCategory!.Id, line.Fund!.Id, ParseAmount(line.AmountText))).ToArray();
            var contribution = new Contribution(Guid.NewGuid(), SelectedBatch.Id, SelectedPerson.Id, SelectedBatch.ServiceDate,
                CurrencyCode.Php, lines, ContributionReference, ContributionMemo);
            await _management.RecordContributionAsync(contribution);
            EntryError = string.Empty;
            ContributionReference = string.Empty;
            ContributionMemo = string.Empty;
            BreakdownLines.Clear();
            AddBreakdownLine();
            await RefreshAnalyticsAsync();
            StatusMessage = $"Recorded {SelectedPerson.DisplayName}'s offering: PHP {contribution.TotalAmount:N2}. This subsidiary-ledger entry is not yet a bank deposit or GL posting.";
        }
        catch (Exception ex) when (ex is OfferingManagementException or InvalidOperationException or ArgumentException)
        {
            EntryError = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RefreshAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        if (_reporting is null) return;
        var asOf = DateOnly.FromDateTime((AnalyticsAsOfDate ?? DateTime.Today).Date);
        var personId = SelectedPerson?.Id;
        var personTotals = await _reporting.GetTotalsAsync(personId, asOf, cancellationToken);
        var churchTotals = await _reporting.GetTotalsAsync(null, asOf, cancellationToken);
        PersonWeekTotal = Format(personTotals.Week);
        PersonMonthTotal = Format(personTotals.Month);
        PersonYearTotal = Format(personTotals.Year);
        PersonAllTimeTotal = Format(personTotals.AllTime);
        ChurchWeekTotal = Format(churchTotals.Week);
        ChurchMonthTotal = Format(churchTotals.Month);
        ChurchYearTotal = Format(churchTotals.Year);
        var points = UsesBreakdownChart
            ? await _reporting.GetCategoryBreakdownAsync(personId, SelectedGranularity, asOf, cancellationToken)
            : await _reporting.GetTrendAsync(personId, SelectedGranularity, asOf, cancellationToken);
        Replace(ChartPoints, points);
        var churchMonthCategoryPoints = await _reporting.GetCategoryBreakdownAsync(
            null,
            OfferingPeriodGranularity.Month,
            asOf,
            cancellationToken);
        Replace(ChurchMonthCategoryPoints, churchMonthCategoryPoints);
        ChartCaption = UsesBreakdownChart
            ? $"{SelectedGranularity} giving-category breakdown for {(SelectedPerson?.DisplayName ?? "all donors")}."
            : $"{SelectedGranularity} offering trend for {(SelectedPerson?.DisplayName ?? "all donors")}.";
    }

    partial void OnSelectedPersonChanged(PersonProfile? value) => _ = RefreshAnalyticsCommand.ExecuteAsync(null);
    partial void OnSelectedGranularityChanged(OfferingPeriodGranularity value) => _ = RefreshAnalyticsCommand.ExecuteAsync(null);
    partial void OnSelectedChartKindChanged(OfferingChartKind value)
    {
        OnPropertyChanged(nameof(UsesBreakdownChart));
        _ = RefreshAnalyticsCommand.ExecuteAsync(null);
    }
    partial void OnAnalyticsAsOfDateChanged(DateTime? value) => _ = RefreshAnalyticsCommand.ExecuteAsync(null);

    private static decimal ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var normalized = text.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }
    private string Format(decimal value) => $"{BaseCurrency} {value:N2}";
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
