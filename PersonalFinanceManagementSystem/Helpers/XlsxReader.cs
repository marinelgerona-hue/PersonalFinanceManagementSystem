using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalFinanceManagementSystem.Helpers;

/// <summary>
/// Reads back the small, single-sheet .xlsx files this app hands out as import templates —
/// not a general-purpose spreadsheet reader. Handles both this app's own writer output
/// (<see cref="XlsxFile"/>, inline strings, dates as text) and the same file after a user
/// opens and re-saves it in Excel (which rewrites string cells through a shared-strings
/// table and auto-converts newly-typed dates into numeric date serials) — both are realistic
/// inputs for "download the template, edit it, upload it". Hand-rolled against the raw
/// OOXML/zip structure, same no-dependency approach as <see cref="XlsxFile"/>.
/// </summary>
public static class XlsxReader
{
    /// <summary>Returns each row (header first) as an array of display-string cell values,
    /// in column order, gaps filled with "". Dates are normalized to "yyyy-MM-dd". Returns
    /// null when the bytes aren't a readable zip/xlsx at all (caller treats that as a
    /// file-level rejection, same as an empty/garbage CSV).</summary>
    public static List<string[]>? TryReadRows(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            var sharedStrings = ReadSharedStrings(zip);
            var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml")
                ?? zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml"));
            if (sheetEntry is null) return null;

            using var reader = new StreamReader(sheetEntry.Open());
            var xml = reader.ReadToEnd();
            return ParseSheet(xml, sharedStrings);
        }
        catch
        {
            return null; // not a valid zip / malformed xlsx — caller rejects the file
        }
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return new List<string>();

        using var reader = new StreamReader(entry.Open());
        var xml = reader.ReadToEnd();

        var strings = new List<string>();
        foreach (Match si in Regex.Matches(xml, "<si[ >].*?</si>", RegexOptions.Singleline))
        {
            // Concatenate every <t>...</t> run inside the <si> (rich text splits one
            // string across multiple runs); a bare <t/> contributes nothing.
            var text = string.Concat(Regex.Matches(si.Value, "<t[^>]*>(.*?)</t>", RegexOptions.Singleline)
                .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)));
            strings.Add(text);
        }
        return strings;
    }

    private static List<string[]> ParseSheet(string xml, List<string> sharedStrings)
    {
        var rows = new List<string[]>();

        foreach (Match rowMatch in Regex.Matches(xml, "<row[^>]*>(.*?)</row>", RegexOptions.Singleline))
        {
            var cells = new Dictionary<int, string>();
            var maxCol = -1;

            foreach (Match cellMatch in Regex.Matches(rowMatch.Groups[1].Value, "<c[^>]*?(?:/>|>.*?</c>)", RegexOptions.Singleline))
            {
                var cellXml = cellMatch.Value;
                var refMatch = Regex.Match(cellXml, "r=\"([A-Z]+)\\d+\"");
                if (!refMatch.Success) continue;
                var col = ColumnIndex(refMatch.Groups[1].Value);
                maxCol = Math.Max(maxCol, col);

                var typeMatch = Regex.Match(cellXml, "t=\"([a-zA-Z]+)\"");
                var type = typeMatch.Success ? typeMatch.Groups[1].Value : "n";

                var value = type switch
                {
                    "inlineStr" => System.Net.WebUtility.HtmlDecode(Regex.Match(cellXml, "<t[^>]*>(.*?)</t>", RegexOptions.Singleline).Groups[1].Value),
                    "s" => ResolveSharedString(cellXml, sharedStrings),
                    "str" or "b" => System.Net.WebUtility.HtmlDecode(Regex.Match(cellXml, "<v>(.*?)</v>", RegexOptions.Singleline).Groups[1].Value),
                    _ => Regex.Match(cellXml, "<v>(.*?)</v>", RegexOptions.Singleline) is { Success: true } vm ? vm.Groups[1].Value : ""
                };

                // A bare numeric cell (type "n", the Excel default) in the Date column (A) is
                // almost always a date-serial number from Excel auto-converting a typed date —
                // normalize it the same way the Date column itself is addressed, by column index.
                if (type == "n" && col == 0 && double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var serial) && serial > 0)
                {
                    value = DateTime.FromOADate(serial).ToString("yyyy-MM-dd");
                }

                cells[col] = value;
            }

            if (maxCol < 0) continue; // a genuinely empty row
            var fields = new string[maxCol + 1];
            for (var c = 0; c <= maxCol; c++) fields[c] = cells.TryGetValue(c, out var v) ? v : "";
            rows.Add(fields);
        }

        return rows;
    }

    private static string ResolveSharedString(string cellXml, List<string> sharedStrings)
    {
        var vMatch = Regex.Match(cellXml, "<v>(.*?)</v>", RegexOptions.Singleline);
        if (!vMatch.Success || !int.TryParse(vMatch.Groups[1].Value, out var index)) return "";
        return index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : "";
    }

    /// <summary>"A"->0, "B"->1, ..., "Z"->25, "AA"->26, matching the spreadsheet's own
    /// column-letter scheme (base-26, no zero digit).</summary>
    private static int ColumnIndex(string letters)
    {
        var col = 0;
        foreach (var ch in letters) col = col * 26 + (ch - 'A' + 1);
        return col - 1;
    }
}
