using ChurchBooks.Accounting.Abstractions;
using ChurchBooks.Accounting.People;

namespace ChurchBooks.Accounting.Importing;

public sealed class PeopleImportRegistrationService
{
    private readonly IPeopleGivingStore _peopleStore;
    private readonly IAdaptiveImportStore _adaptiveStore;
    private readonly PersonIdentityResolver _resolver = new();

    public PeopleImportRegistrationService(IPeopleGivingStore peopleStore, IAdaptiveImportStore adaptiveStore)
    {
        _peopleStore = peopleStore ?? throw new ArgumentNullException(nameof(peopleStore));
        _adaptiveStore = adaptiveStore ?? throw new ArgumentNullException(nameof(adaptiveStore));
    }

    public async Task<IReadOnlyList<PersonImportResolution>> BuildReviewAsync(
        Guid? sourceProfileId,
        IReadOnlyList<PersonImportCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var people = await _peopleStore.GetPeopleAsync(includeArchived: true, cancellationToken: cancellationToken);
        var results = new List<PersonImportResolution>(candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid? linkedId = null;
            if (sourceProfileId.HasValue && sourceProfileId.Value != Guid.Empty && !string.IsNullOrWhiteSpace(candidate.ExternalPersonId))
                linkedId = await _adaptiveStore.FindLinkedPersonIdAsync(sourceProfileId.Value, candidate.ExternalPersonId, cancellationToken);
            results.Add(_resolver.Resolve(candidate, people, linkedId));
        }

        var conflictingRows = FindDuplicateStrongIdentifierRows(candidates);
        return results.Select(result => conflictingRows.Contains(result.Candidate.SourceRowNumber)
            ? new PersonImportResolution(
                result.Candidate,
                PersonImportResolutionStatus.Ambiguous,
                null,
                "A strong identifier is repeated on multiple imported rows. Resolve the duplicate rows before registration.")
            : result).ToArray();
    }

    public async Task<PersonImportApplyResult> RegisterSafePeopleAsync(
        Guid sourceProfileId,
        IReadOnlyList<PersonImportResolution> resolutions,
        CancellationToken cancellationToken = default)
    {
        if (sourceProfileId == Guid.Empty) throw new ArgumentException("A saved source profile is required for registration.", nameof(sourceProfileId));
        ArgumentNullException.ThrowIfNull(resolutions);
        var households = await _peopleStore.GetHouseholdsAsync(includeArchived: false, cancellationToken: cancellationToken);
        var registered = 0;
        var linkedExisting = 0;
        var reviewRequired = 0;

        foreach (var resolution in resolutions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = resolution.Candidate;
            if (resolution.Status == PersonImportResolutionStatus.ExistingExact && resolution.ExistingPerson is not null)
            {
                if (!string.IsNullOrWhiteSpace(candidate.ExternalPersonId))
                    await _adaptiveStore.SavePersonExternalLinkAsync(sourceProfileId, candidate.ExternalPersonId, resolution.ExistingPerson.Id, cancellationToken);
                linkedExisting++;
                continue;
            }
            if (!resolution.CanRegisterNew)
            {
                reviewRequired++;
                continue;
            }

            var householdId = ResolveExistingHousehold(candidate.HouseholdName, households);
            var person = new PersonProfile(
                Guid.NewGuid(),
                candidate.FirstName,
                candidate.LastName,
                candidate.IsMember == true,
                candidate.IsDonor == true,
                candidate.MiddleName,
                candidate.PreferredName,
                candidate.Email,
                candidate.Phone,
                candidate.MemberNumber,
                householdId);
            await _peopleStore.AddPersonAsync(person, cancellationToken);
            if (!string.IsNullOrWhiteSpace(candidate.ExternalPersonId))
                await _adaptiveStore.SavePersonExternalLinkAsync(sourceProfileId, candidate.ExternalPersonId, person.Id, cancellationToken);
            registered++;
        }
        return new PersonImportApplyResult(registered, linkedExisting, reviewRequired);
    }

    private static IReadOnlySet<int> FindDuplicateStrongIdentifierRows(IReadOnlyList<PersonImportCandidate> candidates)
    {
        var rows = new HashSet<int>();
        AddDuplicateRows(candidates, x => NormalizeText(x.ExternalPersonId), rows);
        AddDuplicateRows(candidates, x => NormalizeText(x.MemberNumber), rows);
        AddDuplicateRows(candidates, x => NormalizeText(x.Email), rows);
        AddDuplicateRows(candidates, x => NormalizePhone(x.Phone), rows);
        return rows;
    }

    private static void AddDuplicateRows(
        IReadOnlyList<PersonImportCandidate> candidates,
        Func<PersonImportCandidate, string> selector,
        ISet<int> rows)
    {
        foreach (var group in candidates
            .Select(candidate => (Candidate: candidate, Key: selector(candidate)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            foreach (var item in group) rows.Add(item.Candidate.SourceRowNumber);
        }
    }

    private static string NormalizeText(string value) => value.Trim();

    private static string NormalizePhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 10 ? digits[^10..] : digits;
    }

    private static Guid? ResolveExistingHousehold(string householdName, IReadOnlyList<Household> households)
    {
        if (string.IsNullOrWhiteSpace(householdName)) return null;
        var matches = households.Where(x => string.Equals(x.Name, householdName.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0].Id : null;
    }
}
