using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace ChainPOS.Services.Import;

internal static class SpreadsheetImportReader
{
    public static async Task<IReadOnlyList<Dictionary<string, string>>> ReadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        await using var stream = file.OpenReadStream();
        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ReadXlsx(stream)
            : await ReadCsvAsync(stream, cancellationToken);
    }

    public static decimal ReadDecimal(Dictionary<string, string> row, string key, decimal defaultValue = 0m)
        => decimal.TryParse(Get(row, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || decimal.TryParse(Get(row, key), NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            ? value
            : defaultValue;

    public static decimal? ReadNullableDecimal(Dictionary<string, string> row, string key)
        => decimal.TryParse(Get(row, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || decimal.TryParse(Get(row, key), NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            ? value
            : null;

    public static bool ReadBool(Dictionary<string, string> row, string key, bool defaultValue = true)
    {
        var value = Get(row, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("active", StringComparison.OrdinalIgnoreCase);
    }

    public static string Get(Dictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static async Task<IReadOnlyList<Dictionary<string, string>>> ReadCsvAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<string[]>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            rows.Add(ParseCsvLine(line).ToArray());
        }

        return BuildRows(rows);
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadXlsx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? archive.Entries.FirstOrDefault(x => x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry is null)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<string[]>();
        foreach (var row in document.Descendants(ns + "row"))
        {
            var values = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var columnIndex = GetColumnIndex(reference);
                var type = cell.Attribute("t")?.Value;
                var raw = cell.Element(ns + "v")?.Value ?? cell.Element(ns + "is")?.Element(ns + "t")?.Value ?? string.Empty;
                values[columnIndex] = type == "s" && int.TryParse(raw, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count
                    ? sharedStrings[sharedIndex]
                    : raw;
            }

            if (values.Count > 0)
            {
                var max = values.Keys.Max();
                rows.Add(Enumerable.Range(0, max + 1)
                    .Select(index => values.TryGetValue(index, out var value) ? value : string.Empty)
                    .ToArray());
            }
        }

        return BuildRows(rows);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si")
            .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
            .ToList();
    }

    private static IReadOnlyList<Dictionary<string, string>> BuildRows(IReadOnlyList<string[]> rows)
    {
        var headerIndex = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        var header = rows[headerIndex];
        var keys = header.Select(NormalizeHeader).ToArray();
        var result = new List<Dictionary<string, string>>();
        for (var rowIndex = headerIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var item = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["__row"] = (rowIndex + 1).ToString(CultureInfo.InvariantCulture)
            };
            for (var i = 0; i < keys.Length && i < row.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(keys[i]))
                {
                    item[keys[i]] = row[i];
                }
            }

            result.Add(item);
        }

        return result;
    }

    private static IEnumerable<string> ParseCsvLine(string line)
    {
        var value = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (current == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                value.Append('"');
                i++;
            }
            else if (current == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (current == ',' && !inQuotes)
            {
                yield return value.ToString();
                value.Clear();
            }
            else
            {
                value.Append(current);
            }
        }

        yield return value.ToString();
    }

    private static int GetColumnIndex(string reference)
    {
        var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
        var index = 0;
        foreach (var letter in letters.ToUpperInvariant())
        {
            index = index * 26 + (letter - 'A' + 1);
        }

        return Math.Max(0, index - 1);
    }

    private static string NormalizeHeader(string value)
        => value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
}
