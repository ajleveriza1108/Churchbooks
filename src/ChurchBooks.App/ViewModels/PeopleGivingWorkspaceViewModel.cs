using System.Collections.ObjectModel;
using ChurchBooks.Accounting.Giving;
using ChurchBooks.Accounting.People;
using ChurchBooks.Accounting.Services;
using ChurchBooks.App.Models;
using ChurchBooks.App.Validation;
using ChurchBooks.Data.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChurchBooks.App.ViewModels;

public sealed partial class PeopleGivingWorkspaceViewModel : ObservableObject
{
    private readonly PersonDraftValidator _personValidator = new();
    private readonly HouseholdDraftValidator _householdValidator = new();
    private readonly GivingCategoryDraftValidator _categoryValidator = new();
    private readonly List<PersonListItemViewModel> _allPeople = new();
    private PeopleGivingManagementService? _service;
    private SqlitePeopleGivingStore? _store;
    private Guid? _editingPersonId;
    private Guid? _editingHouseholdId;
    private Guid? _editingCategoryId;

    public ObservableCollection<PersonListItemViewModel> People { get; } = new();
    public ObservableCollection<Household> Households { get; } = new();
    public ObservableCollection<GivingCategory> GivingCategories { get; } = new();

    [ObservableProperty] private string _statusMessage = "People directory is ready.";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _includeArchived;
    [ObservableProperty] private PersonListItemViewModel? _selectedPerson;
    [ObservableProperty] private Household? _selectedHousehold;
    [ObservableProperty] private GivingCategory? _selectedGivingCategory;

    [ObservableProperty] private string _personFirstName = string.Empty;
    [ObservableProperty] private string _personLastName = string.Empty;
    [ObservableProperty] private string _personPreferredName = string.Empty;
    [ObservableProperty] private string _personEmail = string.Empty;
    [ObservableProperty] private string _personPhone = string.Empty;
    [ObservableProperty] private string _personMemberNumber = string.Empty;
    [ObservableProperty] private bool _personIsMember = true;
    [ObservableProperty] private bool _personIsDonor = true;
    [ObservableProperty] private Household? _personHousehold;
    [ObservableProperty] private string _personError = string.Empty;

    [ObservableProperty] private string _householdName = string.Empty;
    [ObservableProperty] private string _householdStatementName = string.Empty;
    [ObservableProperty] private string _householdError = string.Empty;

    [ObservableProperty] private string _categoryCode = string.Empty;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _categoryGroupName = string.Empty;
    [ObservableProperty] private string _categoryDescription = string.Empty;
    [ObservableProperty] private string _categoryError = string.Empty;

    public int ActivePeopleCount => _allPeople.Count(static person => person.Status == PersonStatus.Active);
    public int ActiveHouseholdCount => Households.Count(static household => household.Status == HouseholdStatus.Active);
    public int ActiveGivingCategoryCount => GivingCategories.Count(static category => category.Status == GivingCategoryStatus.Active);

    public async Task InitializeAsync(ChurchBooksDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await new PeopleGivingDatabaseMigrator(database).InitializeAsync(cancellationToken);
        _store = new SqlitePeopleGivingStore(database);
        _service = new PeopleGivingManagementService(_store);
        await RefreshAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_service is null)
        {
            return;
        }

        var people = await _service.GetPeopleAsync(includeArchived: true, cancellationToken);
        var households = await _service.GetHouseholdsAsync(includeArchived: true, cancellationToken);
        var categories = await _service.GetGivingCategoriesAsync(includeArchived: true, cancellationToken);
        var householdNames = households.ToDictionary(static household => household.Id, static household => household.Name);

        _allPeople.Clear();
        foreach (var person in people)
        {
            _allPeople.Add(new PersonListItemViewModel(
                person.Id,
                person.DisplayName,
                person.MemberNumber,
                person.IsMember,
                person.IsDonor,
                person.HouseholdId.HasValue && householdNames.TryGetValue(person.HouseholdId.Value, out var householdName) ? householdName : string.Empty,
                person.Email,
                person.Phone,
                person.Status));
        }

        Households.Clear();
        foreach (var household in households.Where(household => IncludeArchived || household.Status == HouseholdStatus.Active))
        {
            Households.Add(household);
        }

        GivingCategories.Clear();
        foreach (var category in categories.Where(category => IncludeArchived || category.Status == GivingCategoryStatus.Active))
        {
            GivingCategories.Add(category);
        }

        ApplyPeopleFilter();
        NotifySummaryChanged();
    }

    [RelayCommand]
    private void NewPerson()
    {
        _editingPersonId = null;
        SelectedPerson = null;
        PersonFirstName = string.Empty;
        PersonLastName = string.Empty;
        PersonPreferredName = string.Empty;
        PersonEmail = string.Empty;
        PersonPhone = string.Empty;
        PersonMemberNumber = string.Empty;
        PersonIsMember = true;
        PersonIsDonor = true;
        PersonHousehold = null;
        PersonError = string.Empty;
        StatusMessage = "Enter a member or donor. Nothing is posted to accounting.";
    }

    [RelayCommand]
    private async Task EditPersonAsync()
    {
        if (SelectedPerson is null || _store is null)
        {
            return;
        }

        var person = await _store.GetPersonAsync(SelectedPerson.Id);
        if (person is null)
        {
            PersonError = "The selected person no longer exists.";
            return;
        }

        _editingPersonId = person.Id;
        PersonFirstName = person.FirstName;
        PersonLastName = person.LastName;
        PersonPreferredName = person.PreferredName;
        PersonEmail = person.Email;
        PersonPhone = person.Phone;
        PersonMemberNumber = person.MemberNumber;
        PersonIsMember = person.IsMember;
        PersonIsDonor = person.IsDonor;
        PersonHousehold = person.HouseholdId.HasValue ? Households.FirstOrDefault(item => item.Id == person.HouseholdId.Value) : null;
        PersonError = string.Empty;
    }

    [RelayCommand]
    private async Task SavePersonAsync()
    {
        if (_service is null || _store is null)
        {
            PersonError = "People storage is not ready yet.";
            return;
        }

        var draft = new PersonDraft
        {
            FirstName = PersonFirstName.Trim(),
            LastName = PersonLastName.Trim(),
            PreferredName = PersonPreferredName.Trim(),
            Email = PersonEmail.Trim(),
            Phone = PersonPhone.Trim(),
            MemberNumber = PersonMemberNumber.Trim(),
            IsMember = PersonIsMember,
            IsDonor = PersonIsDonor
        };
        var validation = await _personValidator.ValidateAsync(draft);
        if (!validation.IsValid)
        {
            PersonError = string.Join(" ", validation.Errors.Select(static error => error.ErrorMessage).Distinct(StringComparer.Ordinal));
            return;
        }

        try
        {
            var current = _editingPersonId.HasValue ? await _store.GetPersonAsync(_editingPersonId.Value) : null;
            var person = new PersonProfile(
                current?.Id ?? Guid.NewGuid(),
                draft.FirstName,
                draft.LastName,
                draft.IsMember,
                draft.IsDonor,
                preferredName: draft.PreferredName,
                email: draft.Email,
                phone: draft.Phone,
                memberNumber: draft.MemberNumber,
                householdId: PersonHousehold?.Id,
                status: current?.Status ?? PersonStatus.Active,
                archivedUtc: current?.ArchivedUtc);
            await _service.SavePersonAsync(person);
            _editingPersonId = person.Id;
            PersonError = string.Empty;
            await RefreshAsync();
            SelectedPerson = People.FirstOrDefault(item => item.Id == person.Id);
            StatusMessage = $"'{person.DisplayName}' saved. No giving transaction was created.";
        }
        catch (PeopleGivingManagementException ex)
        {
            PersonError = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            PersonError = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ArchiveOrReactivatePersonAsync()
    {
        if (SelectedPerson is null || _service is null)
        {
            return;
        }

        try
        {
            if (SelectedPerson.Status == PersonStatus.Active)
            {
                await _service.ArchivePersonAsync(SelectedPerson.Id);
            }
            else
            {
                await _service.ReactivatePersonAsync(SelectedPerson.Id);
            }
            await RefreshAsync();
            StatusMessage = "Person status updated. Historical identity remains preserved.";
        }
        catch (PeopleGivingManagementException ex)
        {
            PersonError = ex.Message;
        }
    }

    [RelayCommand]
    private void NewHousehold()
    {
        _editingHouseholdId = null;
        SelectedHousehold = null;
        HouseholdName = string.Empty;
        HouseholdStatementName = string.Empty;
        HouseholdError = string.Empty;
    }

    [RelayCommand]
    private void EditHousehold()
    {
        if (SelectedHousehold is null)
        {
            return;
        }
        _editingHouseholdId = SelectedHousehold.Id;
        HouseholdName = SelectedHousehold.Name;
        HouseholdStatementName = SelectedHousehold.StatementName;
        HouseholdError = string.Empty;
    }

    [RelayCommand]
    private async Task SaveHouseholdAsync()
    {
        if (_service is null || _store is null)
        {
            HouseholdError = "Household storage is not ready yet.";
            return;
        }

        var draft = new HouseholdDraft { Name = HouseholdName.Trim(), StatementName = HouseholdStatementName.Trim() };
        var validation = await _householdValidator.ValidateAsync(draft);
        if (!validation.IsValid)
        {
            HouseholdError = string.Join(" ", validation.Errors.Select(static error => error.ErrorMessage).Distinct(StringComparer.Ordinal));
            return;
        }

        try
        {
            var current = _editingHouseholdId.HasValue ? await _store.GetHouseholdAsync(_editingHouseholdId.Value) : null;
            var household = new Household(
                current?.Id ?? Guid.NewGuid(),
                draft.Name,
                draft.StatementName,
                current?.Status ?? HouseholdStatus.Active,
                current?.ArchivedUtc);
            await _service.SaveHouseholdAsync(household);
            _editingHouseholdId = household.Id;
            HouseholdError = string.Empty;
            await RefreshAsync();
            SelectedHousehold = Households.FirstOrDefault(item => item.Id == household.Id);
            StatusMessage = $"Household '{household.Name}' saved.";
        }
        catch (PeopleGivingManagementException ex)
        {
            HouseholdError = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ArchiveOrReactivateHouseholdAsync()
    {
        if (SelectedHousehold is null || _service is null)
        {
            return;
        }
        try
        {
            if (SelectedHousehold.Status == HouseholdStatus.Active)
            {
                await _service.ArchiveHouseholdAsync(SelectedHousehold.Id);
            }
            else
            {
                await _service.ReactivateHouseholdAsync(SelectedHousehold.Id);
            }
            await RefreshAsync();
            StatusMessage = "Household status updated. People records are not deleted.";
        }
        catch (PeopleGivingManagementException ex)
        {
            HouseholdError = ex.Message;
        }
    }

    [RelayCommand]
    private void NewGivingCategory()
    {
        _editingCategoryId = null;
        SelectedGivingCategory = null;
        CategoryCode = string.Empty;
        CategoryName = string.Empty;
        CategoryGroupName = string.Empty;
        CategoryDescription = string.Empty;
        CategoryError = string.Empty;
    }

    [RelayCommand]
    private void EditGivingCategory()
    {
        if (SelectedGivingCategory is null)
        {
            return;
        }
        _editingCategoryId = SelectedGivingCategory.Id;
        CategoryCode = SelectedGivingCategory.Code;
        CategoryName = SelectedGivingCategory.Name;
        CategoryGroupName = SelectedGivingCategory.GroupName;
        CategoryDescription = SelectedGivingCategory.Description;
        CategoryError = string.Empty;
    }

    [RelayCommand]
    private async Task SaveGivingCategoryAsync()
    {
        if (_service is null || _store is null)
        {
            CategoryError = "Giving category storage is not ready yet.";
            return;
        }

        var draft = new GivingCategoryDraft
        {
            Code = CategoryCode.Trim(),
            Name = CategoryName.Trim(),
            GroupName = CategoryGroupName.Trim(),
            Description = CategoryDescription.Trim()
        };
        var validation = await _categoryValidator.ValidateAsync(draft);
        if (!validation.IsValid)
        {
            CategoryError = string.Join(" ", validation.Errors.Select(static error => error.ErrorMessage).Distinct(StringComparer.Ordinal));
            return;
        }

        try
        {
            var current = _editingCategoryId.HasValue ? await _store.GetGivingCategoryAsync(_editingCategoryId.Value) : null;
            var category = new GivingCategory(
                current?.Id ?? Guid.NewGuid(),
                draft.Code,
                draft.Name,
                draft.Description,
                draft.GroupName,
                current?.Status ?? GivingCategoryStatus.Active,
                current?.ArchivedUtc);
            await _service.SaveGivingCategoryAsync(category);
            _editingCategoryId = category.Id;
            CategoryError = string.Empty;
            await RefreshAsync();
            SelectedGivingCategory = GivingCategories.FirstOrDefault(item => item.Id == category.Id);
            StatusMessage = $"Giving category '{category.Name}' saved. Categories do not move money by themselves.";
        }
        catch (PeopleGivingManagementException ex)
        {
            CategoryError = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ArchiveOrReactivateGivingCategoryAsync()
    {
        if (SelectedGivingCategory is null || _service is null)
        {
            return;
        }
        if (SelectedGivingCategory.Status == GivingCategoryStatus.Active)
        {
            await _service.ArchiveGivingCategoryAsync(SelectedGivingCategory.Id);
        }
        else
        {
            await _service.ReactivateGivingCategoryAsync(SelectedGivingCategory.Id);
        }
        await RefreshAsync();
        StatusMessage = "Giving category active status updated. Removed categories disappear from new offering entry but historical giving remains intact.";
    }

    private void ApplyPeopleFilter()
    {
        var search = SearchText.Trim();
        People.Clear();
        foreach (var person in _allPeople.Where(person =>
                     (IncludeArchived || person.Status == PersonStatus.Active) &&
                     (string.IsNullOrWhiteSpace(search) ||
                      person.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                      person.MemberNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                      person.RoleLabel.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                      person.HouseholdName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                      person.Email.Contains(search, StringComparison.OrdinalIgnoreCase))))
        {
            People.Add(person);
        }
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(ActivePeopleCount));
        OnPropertyChanged(nameof(ActiveHouseholdCount));
        OnPropertyChanged(nameof(ActiveGivingCategoryCount));
    }

    partial void OnSearchTextChanged(string value) => ApplyPeopleFilter();

    partial void OnIncludeArchivedChanged(bool value)
    {
        ApplyPeopleFilter();
        _ = RefreshAsync();
    }
}
