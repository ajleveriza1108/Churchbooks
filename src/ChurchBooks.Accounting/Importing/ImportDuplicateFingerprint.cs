using System.Security.Cryptography;
using System.Text;

namespace ChurchBooks.Accounting.Importing;

public static class ImportDuplicateFingerprint
{
    public static string Compute(IReadOnlyList<string> cells, IReadOnlyList<ImportColumnMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(mappings);
        var material = new StringBuilder();
        foreach (var mapping in mappings.Where(x => x.Role != ImportColumnRole.Ignore).OrderBy(x => x.Role).ThenBy(x => x.ColumnIndex))
        {
            var raw = mapping.ColumnIndex < cells.Count ? cells[mapping.ColumnIndex] : string.Empty;
            material.Append((int)mapping.Role).Append('=').Append(Normalize(raw)).Append('|');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()))).ToLowerInvariant();
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
