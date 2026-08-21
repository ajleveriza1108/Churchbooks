using ChurchBooks.Accounting.People;

namespace ChurchBooks.Accounting.Importing;

public sealed class PersonIdentityResolver
{
    public PersonImportResolution Resolve(
        PersonImportCandidate candidate,
        IReadOnlyList<PersonProfile> people,
        Guid? externallyLinkedPersonId = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(people);
        var active = people.Where(x => x.Status == PersonStatus.Active).ToArray();
        var strong = new Dictionary<Guid, (PersonProfile Person, List<string> Reasons)>();

        if (externallyLinkedPersonId.HasValue)
        {
            var linked = active.FirstOrDefault(x => x.Id == externallyLinkedPersonId.Value);
            if (linked is not null) Add(strong, linked, "saved external person ID");
        }
        Match(active, candidate.MemberNumber, x => x.MemberNumber, "member/envelope number", strong, static (a, b) => EqualsText(a, b));
        Match(active, candidate.Email, x => x.Email, "email", strong, static (a, b) => EqualsText(a, b));
        if (!string.IsNullOrWhiteSpace(candidate.Phone))
        {
            foreach (var person in active.Where(x => PhonesMatch(candidate.Phone, x.Phone))) Add(strong, person, "phone");
        }

        if (strong.Count > 1)
            return new PersonImportResolution(candidate, PersonImportResolutionStatus.Ambiguous, null, "Strong identifiers point to different existing people. Review is required; ChurchBooks will not merge them automatically.");
        if (strong.Count == 1)
        {
            var match = strong.Values.Single();
            return new PersonImportResolution(candidate, PersonImportResolutionStatus.ExistingExact, match.Person, "Matched by " + string.Join(", ", match.Reasons.Distinct(StringComparer.OrdinalIgnoreCase)) + ". Existing profile data will not be overwritten automatically.");
        }

        var nameMatches = active.Where(x => SameName(candidate, x)).ToArray();
        if (nameMatches.Length > 0)
            return new PersonImportResolution(candidate, PersonImportResolutionStatus.NeedsReview, nameMatches.Length == 1 ? nameMatches[0] : null, "Name-only similarity is never enough for automatic identity matching. Review the person before registering or linking.");
        if (!candidate.HasUsableName)
            return new PersonImportResolution(candidate, PersonImportResolutionStatus.Invalid, null, "A new person needs at least a usable first and last name.");
        if (!candidate.IsMember.HasValue && !candidate.IsDonor.HasValue)
            return new PersonImportResolution(candidate, PersonImportResolutionStatus.NeedsReview, null, "Choose whether new people from this source are Members, Donors, or Both before registration.");
        if (candidate.IsMember != true && candidate.IsDonor != true)
            return new PersonImportResolution(candidate, PersonImportResolutionStatus.Invalid, null, "A new person must be a Member, Donor, or Both.");
        return new PersonImportResolution(candidate, PersonImportResolutionStatus.NewPerson, null, "No existing person matched a strong identifier. Safe to register after review.");
    }

    private static void Match(
        IEnumerable<PersonProfile> people,
        string incoming,
        Func<PersonProfile, string> selector,
        string reason,
        IDictionary<Guid, (PersonProfile Person, List<string> Reasons)> target,
        Func<string, string, bool> comparer)
    {
        if (string.IsNullOrWhiteSpace(incoming)) return;
        foreach (var person in people.Where(x => comparer(incoming, selector(x)))) Add(target, person, reason);
    }

    private static void Add(IDictionary<Guid, (PersonProfile Person, List<string> Reasons)> target, PersonProfile person, string reason)
    {
        if (!target.TryGetValue(person.Id, out var existing))
        {
            target[person.Id] = (person, new List<string> { reason });
            return;
        }
        existing.Reasons.Add(reason);
        target[person.Id] = existing;
    }

    private static bool SameName(PersonImportCandidate candidate, PersonProfile person) =>
        EqualsText(candidate.FirstName, person.FirstName) && EqualsText(candidate.LastName, person.LastName);

    private static bool EqualsText(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool PhonesMatch(string left, string right)
    {
        var a = Digits(left);
        var b = Digits(right);
        if (a.Length < 7 || b.Length < 7) return false;
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        return a.Length >= 10 && b.Length >= 10 && string.Equals(a[^10..], b[^10..], StringComparison.Ordinal);
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());
}
