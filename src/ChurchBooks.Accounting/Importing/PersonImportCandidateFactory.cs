namespace ChurchBooks.Accounting.Importing;

public sealed class PersonImportCandidateFactory
{
    public PersonImportCandidate Create(
        int sourceRowNumber,
        IReadOnlyList<string> cells,
        IReadOnlyList<ImportColumnMapping> mappings,
        PersonImportDefaultRole defaultRole)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(mappings);
        var byRole = mappings
            .Where(x => x.Role != ImportColumnRole.Ignore)
            .ToDictionary(x => x.Role, x => Cell(cells, x.ColumnIndex));

        var first = Get(byRole, ImportColumnRole.FirstName);
        var middle = Get(byRole, ImportColumnRole.MiddleName);
        var last = Get(byRole, ImportColumnRole.LastName);
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
        {
            var parsed = ParseName(Get(byRole, ImportColumnRole.PersonName));
            if (string.IsNullOrWhiteSpace(first)) first = parsed.First;
            if (string.IsNullOrWhiteSpace(middle)) middle = parsed.Middle;
            if (string.IsNullOrWhiteSpace(last)) last = parsed.Last;
        }

        var member = ParseBoolean(Get(byRole, ImportColumnRole.IsMember));
        var donor = ParseBoolean(Get(byRole, ImportColumnRole.IsDonor));
        ApplyDefaultRole(defaultRole, ref member, ref donor);

        return new PersonImportCandidate(
            sourceRowNumber,
            Get(byRole, ImportColumnRole.ExternalPersonId),
            Get(byRole, ImportColumnRole.MemberNumber),
            first,
            middle,
            last,
            Get(byRole, ImportColumnRole.PreferredName),
            Get(byRole, ImportColumnRole.Email),
            Get(byRole, ImportColumnRole.Phone),
            Get(byRole, ImportColumnRole.HouseholdName),
            member,
            donor);
    }

    private static string Cell(IReadOnlyList<string> cells, int index) =>
        index >= 0 && index < cells.Count ? (cells[index] ?? string.Empty).Trim() : string.Empty;

    private static string Get(IReadOnlyDictionary<ImportColumnRole, string> values, ImportColumnRole role) =>
        values.TryGetValue(role, out var value) ? value : string.Empty;

    private static (string First, string Middle, string Last) ParseName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return (string.Empty, string.Empty, string.Empty);
        if (trimmed.Contains(','))
        {
            var parts = trimmed.Split(',', 2, StringSplitOptions.TrimEntries);
            var given = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return given.Length switch
            {
                0 => (string.Empty, string.Empty, parts[0]),
                1 => (given[0], string.Empty, parts[0]),
                _ => (given[0], string.Join(" ", given.Skip(1)), parts[0])
            };
        }
        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length switch
        {
            0 => (string.Empty, string.Empty, string.Empty),
            1 => (words[0], string.Empty, string.Empty),
            2 => (words[0], string.Empty, words[1]),
            _ => (words[0], string.Join(" ", words.Skip(1).Take(words.Length - 2)), words[^1])
        };
    }

    private static bool? ParseBoolean(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized is "1" or "Y" or "YES" or "TRUE" or "MEMBER" or "DONOR" or "ACTIVE") return true;
        if (normalized is "0" or "N" or "NO" or "FALSE" or "NONE" or "INACTIVE") return false;
        return null;
    }

    private static void ApplyDefaultRole(PersonImportDefaultRole role, ref bool? member, ref bool? donor)
    {
        if (member.HasValue || donor.HasValue) return;
        switch (role)
        {
            case PersonImportDefaultRole.Member: member = true; donor = false; break;
            case PersonImportDefaultRole.Donor: member = false; donor = true; break;
            case PersonImportDefaultRole.Both: member = true; donor = true; break;
        }
    }
}
