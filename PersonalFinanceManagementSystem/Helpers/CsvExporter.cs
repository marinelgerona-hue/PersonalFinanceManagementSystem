using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinanceManagementSystem.Helpers;

/// <summary>
/// Small builder for CSV downloads. Produces UTF-8 with BOM (Excel-friendly)
/// and invariant number/date formatting so files parse the same everywhere.
/// </summary>
public sealed class CsvFile
{
    private readonly List<string[]> _rows = new();

    public CsvFile(params string[] headers)
    {
        _rows.Add(headers);
    }

    /// <summary>Adds a data row. Values are formatted and escaped automatically.</summary>
    public CsvFile AddRow(params object?[] cells)
    {
        _rows.Add(cells.Select(Format).ToArray());
        return this;
    }

    public FileContentResult ToFileResult(string fileName)
    {
        var sb = new StringBuilder();
        foreach (var row in _rows)
        {
            sb.Append(string.Join(",", row.Select(Escape)));
            sb.Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);

        return new FileContentResult(bytes, "text/csv") { FileDownloadName = fileName };
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }
}
