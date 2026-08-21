using System.Text;
using ChurchBooks.Accounting.Importing;

namespace ChurchBooks.Data.Importing;

public sealed class StandardImportTemplateWriter
{
    public async Task WriteCsvAsync(StandardImportTemplateDefinition template, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var header = string.Join(",", template.Headers.Select(Escape));
        await File.WriteAllTextAsync(fullPath, header + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
    }

    private static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
