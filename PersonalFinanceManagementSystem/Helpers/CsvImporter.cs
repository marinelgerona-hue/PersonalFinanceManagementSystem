using System.Globalization;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Helpers;

public enum ImportRowStatus { Valid, Duplicate, Rejected }

/// <summary>One parsed line of an uploaded import file.</summary>
public class ImportRow
{
    public int LineNumber { get; set; }
    public string RawLine { get; set; } = string.Empty;

    public RecurringTransactionType Type { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;

    public ImportRowStatus Status { get; set; }
    public string? RejectReason { get; set; }
}

/// <summary>Outcome of parsing one uploaded file.</summary>
public class ImportResult
{
    /// <summary>Non-null means the whole file was rejected outright — nothing below applies.</summary>
    public string? FileRejectedReason { get; set; }
    public int TotalDataRows { get; set; }
    public List<ImportRow> Valid { get; set; } = new();
    public List<ImportRow> Duplicates { get; set; } = new();
    public List<ImportRow> Rejected { get; set; } = new();
}

/// <summary>
/// Parses the app's bank-statement import template (Date,Type,Category,Amount,Note),
/// validates every row, flags duplicates against the account's existing records, and
/// rejects files that look like unedited sample/test data. Hand-rolled CSV splitting
/// (handles quoted fields) — no third-party package, same philosophy as XlsxFile/PdfFile.
/// </summary>
public static class CsvImporter
{
    public static readonly string[] ExpectedHeader = { "Date", "Type", "Category", "Amount", "Note" };

    /// <summary>Case-insensitive substrings that mark a row as placeholder/test data rather
    /// than a real transaction.</summary>
    private static readonly string[] PlaceholderTokens =
    {
        "test", "sample", "lorem ipsum", "dummy", "example", "asdf", "xxx",
        "n/a", "placeholder", "replace this", "replace with your own"
    };

    /// <summary>If at least this share of data rows are rejected specifically as
    /// placeholder/test data, the whole file is rejected rather than partially imported.</summary>
    private const double PlaceholderFileRejectShare = 0.5;

    /// <summary>Parses a .csv upload (text/comma-separated, quoted fields honored).</summary>
    public static ImportResult Parse(
        string csvText,
        IReadOnlyCollection<(DateTime Date, decimal Amount, string Label, bool IsIncome)> existingTransactions)
    {
        var lines = csvText
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0) return Rejected("The file is empty.");

        var rows = lines.Select(SplitLine).ToList();
        return ParseRows(rows, existingTransactions);
    }

    /// <summary>Parses a .xlsx upload — either this app's own generated sample, or that same
    /// file after being edited and re-saved in Excel (shared strings, auto-converted date
    /// serials). Returns a file-level rejection when the bytes aren't a readable workbook.</summary>
    public static ImportResult ParseXlsx(
        byte[] bytes,
        IReadOnlyCollection<(DateTime Date, decimal Amount, string Label, bool IsIncome)> existingTransactions)
    {
        var rows = XlsxReader.TryReadRows(bytes);
        if (rows is null)
        {
            return Rejected("This doesn't look like a valid .xlsx file — it may be corrupted or not actually an Excel workbook.");
        }
        if (rows.Count == 0) return Rejected("The file is empty.");

        return ParseRows(rows, existingTransactions);
    }

    private static ImportResult Rejected(string reason) => new() { FileRejectedReason = reason };

    /// <summary>Shared validation/dedup/placeholder-detection pipeline — <paramref name="rows"/>
    /// is every row already split into fields, header row first, exactly as either the CSV
    /// splitter or <see cref="XlsxReader"/> produces it.</summary>
    private static ImportResult ParseRows(
        List<string[]> rows,
        IReadOnlyCollection<(DateTime Date, decimal Amount, string Label, bool IsIncome)> existingTransactions)
    {
        var result = new ImportResult();

        var header = rows[0].Select(h => h.Trim()).ToArray();
        if (header.Length != ExpectedHeader.Length ||
            !header.Select(h => h.ToLowerInvariant()).SequenceEqual(ExpectedHeader.Select(h => h.ToLowerInvariant())))
        {
            result.FileRejectedReason =
                $"This doesn't look like a valid import file — the header row must be: {string.Join(",", ExpectedHeader)}. " +
                "Download the sample file for the exact format.";
            return result;
        }

        var dataRows = rows.Skip(1).ToList();
        result.TotalDataRows = dataRows.Count;
        if (dataRows.Count == 0)
        {
            result.FileRejectedReason = "No data rows found below the header.";
            return result;
        }

        var seenInFile = new HashSet<(DateTime, decimal, bool, string)>();
        var placeholderRejectCount = 0;

        for (var i = 0; i < dataRows.Count; i++)
        {
            var lineNumber = i + 2; // +1 for header, +1 for 1-based display
            var fields = dataRows[i];
            var row = ParseRow(lineNumber, fields);

            if (row.Status == ImportRowStatus.Rejected)
            {
                if (row.RejectReason!.Contains("placeholder", StringComparison.OrdinalIgnoreCase)) placeholderRejectCount++;
                result.Rejected.Add(row);
                continue;
            }

            var key = (row.Date.Date, row.Amount, row.Type == RecurringTransactionType.Income, row.Label.Trim().ToLowerInvariant());
            var isDuplicate = existingTransactions.Any(e =>
                    e.Date.Date == key.Item1 && e.Amount == key.Item2 && e.IsIncome == key.Item3 &&
                    e.Label.Trim().Equals(key.Item4, StringComparison.OrdinalIgnoreCase))
                || !seenInFile.Add(key);

            if (isDuplicate)
            {
                row.Status = ImportRowStatus.Duplicate;
                result.Duplicates.Add(row);
            }
            else
            {
                row.Status = ImportRowStatus.Valid;
                result.Valid.Add(row);
            }
        }

        // Checked before the generic "every row failed" catch-all so a file made entirely of
        // placeholder rows (e.g. the unedited sample file) gets the specific, actionable reason.
        if (placeholderRejectCount / (double)dataRows.Count >= PlaceholderFileRejectShare)
        {
            result.FileRejectedReason =
                "This file looks like sample/test data, not a real statement — please replace the example rows " +
                "with your own transactions before importing.";
            return result;
        }

        if (result.Valid.Count == 0 && result.Duplicates.Count == 0)
        {
            result.FileRejectedReason = "Every row in this file failed validation — nothing was imported.";
            return result;
        }

        return result;
    }

    private static ImportRow ParseRow(int lineNumber, string[] fields)
    {
        var row = new ImportRow { LineNumber = lineNumber, RawLine = string.Join(",", fields) };

        if (fields.Length != 5)
        {
            return Reject(row, $"Expected 5 columns (Date,Type,Category,Amount,Note), found {fields.Length}.");
        }

        var (dateText, typeText, label, amountText, note) = (fields[0].Trim(), fields[1].Trim(), fields[2].Trim(), fields[3].Trim(), fields[4].Trim());

        if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return Reject(row, $"\"{dateText}\" is not a valid date.");
        }
        if (date.Year < 2000 || date.Date > DateTime.Today.AddDays(1))
        {
            return Reject(row, $"\"{dateText}\" is not a plausible transaction date.");
        }

        RecurringTransactionType type;
        if (typeText.Equals("Income", StringComparison.OrdinalIgnoreCase)) type = RecurringTransactionType.Income;
        else if (typeText.Equals("Expense", StringComparison.OrdinalIgnoreCase)) type = RecurringTransactionType.Expense;
        else return Reject(row, $"Type must be \"Income\" or \"Expense\", found \"{typeText}\".");

        if (label.Length == 0)
        {
            return Reject(row, "Category / source is blank.");
        }

        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            return Reject(row, $"\"{amountText}\" is not a valid positive amount.");
        }
        if (amount > 1_000_000_000m)
        {
            return Reject(row, $"\"{amountText}\" is not a plausible transaction amount.");
        }

        if (ContainsPlaceholder(label) || ContainsPlaceholder(note))
        {
            return Reject(row, "Looks like placeholder/test data, not a real transaction.");
        }

        row.Date = date.Date;
        row.Type = type;
        row.Label = label;
        row.Amount = amount;
        row.Note = note.Length > 300 ? note[..300] : note;
        return row;
    }

    private static ImportRow Reject(ImportRow row, string reason)
    {
        row.Status = ImportRowStatus.Rejected;
        row.RejectReason = reason;
        return row;
    }

    private static bool ContainsPlaceholder(string text)
        => PlaceholderTokens.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));

    /// <summary>Splits one CSV line into fields, honoring double-quoted fields
    /// (with "" as an escaped quote) so a Note can safely contain commas.</summary>
    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
