using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.Importing;
using ChurchBooks.App.Models;
using ChurchBooks.Data.Importing;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class ImportWorkspaceViewModel : ObservableObject
{
    private readonly SpreadsheetImportReader _reader = new();
    private readonly AdaptiveImportAnalyzer _adaptiveAnalyzer = new();
    private readonly PersonImportCandidateFactory _personCandidateFactory = new();
    private readonly StandardImportTemplateWriter _templateWriter = new();
    private SqliteSmartImportStore? _store;
    private SqliteAdaptiveImportStore? _adaptiveStore;
    private PeopleImportRegistrationService? _peopleRegistration;
    private TabularImportDocument? _document;
    private AdaptiveImportAnalysis? _analysis;

    public ObservableCollection<ImportColumnMappingItem> Mappings { get; } = new();
    public ObservableCollection<ImportPreviewRowItem> PreviewRows { get; } = new();
    public ObservableCollection<ImportSourceProfile> MatchingProfiles { get; } = new();
    public ObservableCollection<PersonImportReviewItem> PersonReview { get; } = new();
    public IReadOnlyList<ImportColumnRole> AvailableRoles { get; } = Enum.GetValues<ImportColumnRole>();
    public IReadOnlyList<PersonImportDefaultRole> PersonDefaultRoles { get; } = Enum.GetValues<PersonImportDefaultRole>();
    public IReadOnlyList<StandardImportTemplateDefinition> StandardTemplates { get; } = ChurchBooksStandardImportTemplates.All;

    [ObservableProperty] private string _selectedFile = "No file selected";
    [ObservableProperty] private string _worksheetName = string.Empty;
    [ObservableProperty] private string _headerRowDisplay = "—";
    [ObservableProperty] private string _detectedPurpose = "Unknown";
    [ObservableProperty] private string _detectionConfidence = "—";
    [ObservableProperty] private string _sourceProfileStatus = "No matching saved profile";
    [ObservableProperty] private string _statusMessage = "Choose a CSV, XLS, or XLSX file. ChurchBooks will analyze it before anything is staged.";
    [ObservableProperty] private string _templateName = "Import source profile";
    [ObservableProperty] private int _sourceRowCount;
    [ObservableProperty] private int _duplicateCount;
    [ObservableProperty] private bool _hasAnalysis;
    [ObservableProperty] private ImportSourceProfile? _selectedSourceProfile;
    [ObservableProperty] private StandardImportTemplateDefinition? _selectedStandardTemplate;
    [ObservableProperty] private PersonImportDefaultRole _selectedPersonDefaultRole = PersonImportDefaultRole.Ask;

    public bool IsPeopleDirectory => _analysis?.Purpose.Purpose == ImportPurpose.PeopleDirectory;
    public bool HasPersonReview => PersonReview.Count > 0;

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await new Phase9DatabaseMigrator(database).InitializeAsync(cancellationToken);
        _store = new SqliteSmartImportStore(database);
        _adaptiveStore = new SqliteAdaptiveImportStore(database);
        _peopleRegistration = new PeopleImportRegistrationService(new SqlitePeopleGivingStore(database), _adaptiveStore);
        SelectedStandardTemplate = StandardTemplates.FirstOrDefault();
    }

    public async Task LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        StatusMessage = "Analyzing spreadsheet locally...";
        var document = await _reader.ReadAsync(path, cancellationToken: cancellationToken);
        var analysis = _adaptiveAnalyzer.Analyze(document.Headers, document.Rows);
        var profiles = await _adaptiveStore!.FindSourceProfilesAsync(analysis.SourceSignature, cancellationToken);
        var legacy = profiles.Count == 0 ? await _store!.FindTemplateAsync(analysis.SourceSignature, cancellationToken) : null;

        _document = document;
        _analysis = analysis;
        SelectedFile = document.FileName;
        WorksheetName = string.IsNullOrWhiteSpace(document.WorksheetName) ? "CSV" : document.WorksheetName;
        HeaderRowDisplay = document.HeaderRowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceRowCount = document.Rows.Count;
        DetectedPurpose = PurposeLabel(analysis.Purpose.Purpose);
        DetectionConfidence = analysis.Purpose.Confidence.ToString("P0", System.Globalization.CultureInfo.CurrentCulture);
        TemplateName = BuildDefaultProfileName(document.FileName, analysis.Purpose.Purpose);

        MatchingProfiles.Clear();
        foreach (var profile in profiles) MatchingProfiles.Add(profile);
        SelectedSourceProfile = profiles.Count == 1 ? profiles[0] : null;
        SelectedPersonDefaultRole = SelectedSourceProfile?.DefaultPersonRole ?? PersonImportDefaultRole.Ask;
        SourceProfileStatus = profiles.Count switch
        {
            0 => "New layout — review mappings; save a source profile if you will use this format again.",
            1 => "Recognized saved source profile: " + profiles[0].Name,
            _ => $"{profiles.Count} saved profiles match this layout. Choose the intended profile."
        };

        BuildMappings(analysis, SelectedSourceProfile, legacy);
        await RefreshPreviewAsync(cancellationToken);
        await RefreshPeopleReviewAsync(cancellationToken);
        HasAnalysis = true;
        OnPropertyChanged(nameof(IsPeopleDirectory));
        OnPropertyChanged(nameof(HasPersonReview));
        StatusMessage = BuildAnalysisStatus(analysis, profiles.Count);
    }

    public Task SaveStandardTemplateAsync(StandardImportTemplateDefinition template, string path, CancellationToken cancellationToken = default) =>
        _templateWriter.WriteCsvAsync(template, path, cancellationToken);

    [RelayCommand]
    private async Task ApplySelectedProfileAsync(CancellationToken cancellationToken = default)
    {
        EnsureAnalysis();
        if (SelectedSourceProfile is null) throw new InvalidOperationException("Choose a matching source profile first.");
        if (!string.Equals(SelectedSourceProfile.SourceSignature, _analysis!.SourceSignature, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected source profile does not match this file layout.");
        ApplyProfileMappings(SelectedSourceProfile);
        SelectedPersonDefaultRole = SelectedSourceProfile.DefaultPersonRole;
        await _adaptiveStore!.TouchSourceProfileAsync(SelectedSourceProfile.Id, DateTimeOffset.UtcNow, cancellationToken);
        await RefreshPreviewAsync(cancellationToken);
        await RefreshPeopleReviewAsync(cancellationToken);
        SourceProfileStatus = "Applied saved source profile: " + SelectedSourceProfile.Name;
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (_document is null || _store is null) return;
        var mappings = Mappings.Select(x => x.ToDomain()).ToArray();
        var rows = _document.Rows.Take(50).Select((cells, i) => new
        {
            Row = _document.HeaderRowNumber + i + 1,
            Cells = cells,
            Fingerprint = ImportDuplicateFingerprint.Compute(cells, mappings)
        }).ToArray();
        var existing = await _store.FindExistingFingerprintsAsync(rows.Select(x => x.Fingerprint), cancellationToken);
        var localCounts = rows.GroupBy(x => x.Fingerprint, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        PreviewRows.Clear();
        DuplicateCount = 0;
        foreach (var row in rows)
        {
            var duplicate = existing.Contains(row.Fingerprint) || localCounts[row.Fingerprint] > 1;
            if (duplicate) DuplicateCount++;
            PreviewRows.Add(new ImportPreviewRowItem(row.Row, BuildPreview(row.Cells, mappings), duplicate));
        }
    }

    [RelayCommand]
    private async Task SaveMappingTemplateAsync(CancellationToken cancellationToken = default)
    {
        EnsureAnalysis();
        var mappings = ValidatedMappings();
        var profileId = SelectedSourceProfile?.Id ?? Guid.NewGuid();
        var name = string.IsNullOrWhiteSpace(TemplateName) ? BuildDefaultProfileName(SelectedFile, _analysis!.Purpose.Purpose) : TemplateName.Trim();
        var profile = new ImportSourceProfile(
            profileId,
            name,
            _analysis!.Purpose.Purpose,
            _analysis.SourceSignature,
            mappings,
            SelectedPersonDefaultRole,
            DateTimeOffset.UtcNow);
        await _adaptiveStore!.SaveSourceProfileAsync(profile, cancellationToken);
        await _store!.SaveTemplateAsync(new ImportMappingTemplate(Guid.NewGuid(), name, _analysis.SourceSignature, mappings), cancellationToken);
        SelectedSourceProfile = profile;
        MatchingProfiles.Clear();
        foreach (var match in await _adaptiveStore.FindSourceProfilesAsync(_analysis.SourceSignature, cancellationToken)) MatchingProfiles.Add(match);
        SourceProfileStatus = "Saved source profile: " + profile.Name;
        await RefreshPeopleReviewAsync(cancellationToken);
        StatusMessage = "Source profile saved. ChurchBooks can recognize this layout next time without forcing this format on other imports.";
    }

    [RelayCommand]
    private async Task RefreshPeopleReviewAsync(CancellationToken cancellationToken = default)
    {
        PersonReview.Clear();
        if (_document is null || _analysis?.Purpose.Purpose != ImportPurpose.PeopleDirectory || _peopleRegistration is null) return;
        var mappings = Mappings.Select(x => x.ToDomain()).ToArray();
        var candidates = _document.Rows.Select((cells, index) =>
            _personCandidateFactory.Create(_document.HeaderRowNumber + index + 1, cells, mappings, SelectedPersonDefaultRole)).ToArray();
        var resolutions = await _peopleRegistration.BuildReviewAsync(SelectedSourceProfile?.Id, candidates, cancellationToken);
        foreach (var resolution in resolutions)
        {
            PersonReview.Add(new PersonImportReviewItem(
                resolution.Candidate.SourceRowNumber,
                resolution.Candidate.DisplayName,
                resolution.Candidate.MemberNumber,
                resolution.Candidate.Email,
                resolution.Status.ToString(),
                resolution.ExistingPerson?.DisplayName ?? string.Empty,
                resolution.Reason,
                resolution));
        }
        OnPropertyChanged(nameof(HasPersonReview));
    }

    [RelayCommand]
    private async Task RegisterSafePeopleAsync(CancellationToken cancellationToken = default)
    {
        EnsureAnalysis();
        if (_analysis!.Purpose.Purpose != ImportPurpose.PeopleDirectory)
            throw new InvalidOperationException("Register Safe People is available only for files detected and reviewed as People Directory data.");
        if (SelectedSourceProfile is null)
        {
            await SaveMappingTemplateAsync(cancellationToken);
            if (SelectedSourceProfile is null) throw new InvalidOperationException("Save a source profile before person registration.");
        }
        await RefreshPeopleReviewAsync(cancellationToken);
        var result = await _peopleRegistration!.RegisterSafePeopleAsync(
            SelectedSourceProfile.Id,
            PersonReview.Select(x => x.Resolution).ToArray(),
            cancellationToken);
        await RefreshPeopleReviewAsync(cancellationToken);
        StatusMessage = $"Registered {result.RegisteredCount:N0} new person(s), remembered {result.LinkedExistingCount:N0} exact existing link(s), and left {result.ReviewRequiredCount:N0} row(s) for review. Existing person profiles were not overwritten.";
    }

    [RelayCommand]
    private async Task ApproveAndStageAsync(CancellationToken cancellationToken = default)
    {
        EnsureAnalysis();
        var mappings = ValidatedMappings();
        var fingerprints = _document!.Rows.Select(cells => ImportDuplicateFingerprint.Compute(cells, mappings)).ToArray();
        var existing = await _store!.FindExistingFingerprintsAsync(fingerprints, cancellationToken);
        var localCounts = fingerprints.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var staged = _document.Rows.Select((cells, index) =>
        {
            var fp = fingerprints[index];
            var normalized = mappings.Where(x => x.Role != ImportColumnRole.Ignore).ToDictionary(x => x.Role.ToString(), x => x.ColumnIndex < cells.Count ? cells[x.ColumnIndex] : string.Empty);
            return new ImportStagedRow(Guid.NewGuid(), _document.HeaderRowNumber + index + 1, fp, JsonSerializer.Serialize(normalized), existing.Contains(fp) || localCounts[fp] > 1);
        }).ToArray();
        var session = new ImportSession(Guid.NewGuid(), _document.FileName, _document.FileHash, _document.WorksheetName, _document.SourceKind, _document.Rows.Count, staged);
        await _store.StageSessionAsync(session, cancellationToken);
        DuplicateCount = session.DuplicateCount;
        if (SelectedSourceProfile is not null)
            await _adaptiveStore!.TouchSourceProfileAsync(SelectedSourceProfile.Id, DateTimeOffset.UtcNow, cancellationToken);
        StatusMessage = $"Staged {session.Rows.Count:N0} row(s) for review; {session.DuplicateCount:N0} potential duplicate(s). No journal, bank posting, giving posting, or automatic person merge was created.";
    }

    private void BuildMappings(AdaptiveImportAnalysis analysis, ImportSourceProfile? profile, ImportMappingTemplate? legacy)
    {
        Mappings.Clear();
        foreach (var column in analysis.ColumnAnalysis.Columns)
        {
            var savedRole = profile?.Mappings.FirstOrDefault(x => x.ColumnIndex == column.ColumnIndex)?.Role
                ?? legacy?.Mappings.FirstOrDefault(x => x.ColumnIndex == column.ColumnIndex)?.Role;
            Mappings.Add(new ImportColumnMappingItem
            {
                ColumnIndex = column.ColumnIndex,
                Header = column.Header,
                Confidence = column.Confidence,
                Samples = string.Join(" | ", column.Samples),
                Role = savedRole ?? column.SuggestedRole
            });
        }
    }

    private void ApplyProfileMappings(ImportSourceProfile profile)
    {
        foreach (var item in Mappings)
        {
            var saved = profile.Mappings.FirstOrDefault(x => x.ColumnIndex == item.ColumnIndex);
            item.Role = saved?.Role ?? ImportColumnRole.Ignore;
        }
    }

    private ImportColumnMapping[] ValidatedMappings()
    {
        var mappings = Mappings.Select(x => x.ToDomain()).ToArray();
        if (!mappings.Any(x => x.Role != ImportColumnRole.Ignore)) throw new InvalidOperationException("Map at least one column before saving or staging.");
        var activeRoles = mappings.Where(x => x.Role != ImportColumnRole.Ignore).Select(x => x.Role).ToArray();
        if (activeRoles.Distinct().Count() != activeRoles.Length) throw new InvalidOperationException("Each active import role can be mapped only once.");
        return mappings;
    }

    private static string BuildPreview(IReadOnlyList<string> cells, IReadOnlyList<ImportColumnMapping> mappings) =>
        string.Join("  •  ", mappings.Where(x => x.Role != ImportColumnRole.Ignore).Take(5).Select(x => $"{x.Role}: {(x.ColumnIndex < cells.Count ? cells[x.ColumnIndex] : string.Empty)}"));

    private static string BuildDefaultProfileName(string fileName, ImportPurpose purpose) =>
        $"{PurposeLabel(purpose)} — {Path.GetFileNameWithoutExtension(fileName)}";

    private static string PurposeLabel(ImportPurpose purpose) => purpose switch
    {
        ImportPurpose.PeopleDirectory => "People Directory",
        ImportPurpose.Giving => "Giving",
        ImportPurpose.BankStatement => "Bank Statement",
        ImportPurpose.GeneralLedger => "General Ledger",
        _ => "Unclassified Import"
    };

    private static string BuildAnalysisStatus(AdaptiveImportAnalysis analysis, int matchingProfileCount)
    {
        var profileText = matchingProfileCount switch
        {
            0 => "No saved profile was required; review the suggestions and save one only if useful.",
            1 => "One saved source profile was recognized and applied.",
            _ => "Multiple saved profiles match; choose the intended profile before staging."
        };
        var warnings = analysis.ColumnAnalysis.Warnings.Count == 0 ? string.Empty : " " + string.Join(" ", analysis.ColumnAnalysis.Warnings);
        return analysis.Purpose.Explanation + " " + profileText + warnings;
    }

    private void EnsureInitialized()
    {
        if (_store is null || _adaptiveStore is null || _peopleRegistration is null)
            throw new InvalidOperationException("Adaptive Smart Import is not initialized.");
    }

    private void EnsureAnalysis()
    {
        EnsureInitialized();
        if (_document is null || _analysis is null) throw new InvalidOperationException("Analyze a file first.");
    }
}
