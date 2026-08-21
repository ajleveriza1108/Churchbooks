using System.Security.Cryptography;
using System.Text;
using ChurchBooks.Accounting.Importing;
using ExcelDataReader;

namespace ChurchBooks.Data.Importing;

public sealed class SpreadsheetImportReader
{
    public const int MaxRows = 10000;
    public const int MaxColumns = 100;
    public const int MaxCellCharacters = 4000;
    public const int HeaderScanRows = 25;

    public async Task<TabularImportDocument> ReadAsync(string path, string? worksheetName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Import file was not found.", fullPath);
        var sourceKind = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".csv" => ImportSourceKind.Csv,
            ".xls" => ImportSourceKind.Xls,
            ".xlsx" => ImportSourceKind.Xlsx,
            _ => throw new NotSupportedException("ChurchBooks Smart Import supports .csv, .xls, and .xlsx files.")
        };
        var hash = await ComputeHashAsync(fullPath, cancellationToken);
        return sourceKind == ImportSourceKind.Csv
            ? await ReadCsvAsync(fullPath, hash, cancellationToken)
            : await Task.Run(() => ReadExcel(fullPath, hash, sourceKind, worksheetName, cancellationToken), cancellationToken);
    }

    private static async Task<TabularImportDocument> ReadCsvAsync(string path, string hash, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, useAsync: true);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("The CSV file is empty.");
        var delimiter = DetectDelimiter(text);
        var parsed = ParseDelimitedText(text, delimiter);
        if (parsed.Count > MaxRows + HeaderScanRows) throw new InvalidDataException($"Import files are limited to {MaxRows:N0} data rows.");
        return BuildDocument(path, hash, string.Empty, ImportSourceKind.Csv, parsed);
    }

    private static TabularImportDocument ReadExcel(string path, string hash, ImportSourceKind kind, string? worksheetName, CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var candidates = new List<TabularImportDocument>();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(worksheetName) && !string.Equals(reader.Name, worksheetName, StringComparison.OrdinalIgnoreCase)) continue;
            var rows = ReadWorksheetRows(reader, cancellationToken);
            if (rows.Count == 0) continue;
            candidates.Add(BuildDocument(path, hash, reader.Name ?? string.Empty, kind, rows));
            if (!string.IsNullOrWhiteSpace(worksheetName)) break;
        } while (reader.NextResult());

        if (candidates.Count == 0)
            throw new InvalidDataException(string.IsNullOrWhiteSpace(worksheetName) ? "The workbook has no readable worksheet rows." : $"Worksheet '{worksheetName}' was not found or is empty.");
        if (!string.IsNullOrWhiteSpace(worksheetName)) return candidates[0];
        return candidates
            .OrderByDescending(x => SmartImportAnalyzer.ScoreHeaderCandidate(x.Headers))
            .ThenByDescending(x => x.Rows.Count)
            .First();
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadWorksheetRows(IExcelDataReader reader, CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyList<string>>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.FieldCount > MaxColumns) throw new InvalidDataException($"Import files are limited to {MaxColumns} columns.");
            var row = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++) row[i] = NormalizeCell(reader.GetValue(i));
            rows.Add(row);
            if (rows.Count > MaxRows + HeaderScanRows) throw new InvalidDataException($"Import files are limited to {MaxRows:N0} data rows plus up to {HeaderScanRows} heading rows.");
        }
        return rows;
    }

    private static TabularImportDocument BuildDocument(string path, string hash, string sheet, ImportSourceKind kind, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0) throw new InvalidDataException("The import file has no rows.");
        var headerIndex = FindHeaderRowIndex(rows);
        var width = rows.Skip(headerIndex).Take(Math.Min(rows.Count - headerIndex, MaxRows + 1)).Max(x => x.Count);
        if (width == 0) throw new InvalidDataException("The import file has no columns.");
        if (width > MaxColumns) throw new InvalidDataException($"Import files are limited to {MaxColumns} columns.");
        var headerRow = rows[headerIndex];
        var headers = Enumerable.Range(0, width)
            .Select(i => i < headerRow.Count && !string.IsNullOrWhiteSpace(headerRow[i]) ? headerRow[i].Trim() : $"Column {i + 1}")
            .ToArray();
        var data = rows
            .Skip(headerIndex + 1)
            .Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c)))
            .Take(MaxRows + 1)
            .Select(r => (IReadOnlyList<string>)Enumerable.Range(0, width).Select(i => i < r.Count ? r[i] : string.Empty).ToArray())
            .ToArray();
        if (data.Length > MaxRows) throw new InvalidDataException($"Import files are limited to {MaxRows:N0} data rows.");
        return new TabularImportDocument(Path.GetFileName(path), hash, sheet, kind, headers, data, headerIndex + 1);
    }

    internal static int FindHeaderRowIndex(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0) throw new ArgumentException("At least one row is required.", nameof(rows));
        var limit = Math.Min(HeaderScanRows, rows.Count);
        var bestIndex = -1;
        var bestScore = -1;
        for (var i = 0; i < limit; i++)
        {
            if (!rows[i].Any(x => !string.IsNullOrWhiteSpace(x))) continue;
            var score = SmartImportAnalyzer.ScoreHeaderCandidate(rows[i]);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        if (bestIndex >= 0 && bestScore > 0) return bestIndex;
        for (var i = 0; i < limit; i++) if (rows[i].Any(x => !string.IsNullOrWhiteSpace(x))) return i;
        throw new InvalidDataException("No usable header row was found in the first 25 rows.");
    }

    private static string NormalizeCell(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
        text = text.Trim();
        return text.Length <= MaxCellCharacters ? text : text[..MaxCellCharacters];
    }

    private static char DetectDelimiter(string text)
    {
        var sample = string.Join("\n", text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Take(10));
        var candidates = new[] { ',', '\t', ';' };
        return candidates.OrderByDescending(c => sample.Count(ch => ch == c)).First();
    }

    internal static IReadOnlyList<string> ParseDelimitedLine(string line, char delimiter) =>
        ParseDelimitedText(line, delimiter).Single();

    internal static IReadOnlyList<IReadOnlyList<string>> ParseDelimitedText(string text, char delimiter)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = !quoted;
                continue;
            }
            if (ch == delimiter && !quoted)
            {
                row.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            if ((ch == '\r' || ch == '\n') && !quoted)
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(current.ToString().Trim());
                current.Clear();
                if (row.Any(cell => !string.IsNullOrWhiteSpace(cell))) rows.Add(row.ToArray());
                row.Clear();
                if (rows.Count > MaxRows + HeaderScanRows) throw new InvalidDataException($"Import files are limited to {MaxRows:N0} data rows plus up to {HeaderScanRows} heading rows.");
                continue;
            }
            current.Append(ch);
        }
        if (quoted) throw new InvalidDataException("The CSV file contains an unterminated quoted field.");
        row.Add(current.ToString().Trim());
        if (row.Any(cell => !string.IsNullOrWhiteSpace(cell))) rows.Add(row.ToArray());
        return rows;
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, useAsync: true);
        return Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();
    }
}
