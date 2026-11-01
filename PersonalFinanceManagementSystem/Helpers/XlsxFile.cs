using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinanceManagementSystem.Helpers;

/// <summary>What a column's numbers mean, so the cell gets the right Excel number format
/// (thousands separators always come along with Currency/Integer/Percent automatically —
/// that's just what those number formats do).</summary>
public enum XlsxColumn
{
    /// <summary>Plain text (names, categories, notes, dates — dates are kept as
    /// "yyyy-MM-dd" text rather than Excel's native date serials, to stay simple).</summary>
    Text,

    /// <summary>A money amount: shown with the file's currency symbol, thousands separators
    /// and two decimal places, e.g. "$1,234.56".</summary>
    Currency,

    /// <summary>A plain whole number with thousands separators, e.g. "1,234".</summary>
    Integer,

    /// <summary>A number that's already the percentage itself (59.0, not 0.59) — shown
    /// with one decimal place and a literal "%", e.g. "59.0%".</summary>
    Percent
}

/// <summary>
/// Small builder for real, nicely formatted .xlsx downloads — same shape as the old CsvFile
/// (headers, then AddRow per row, then ToFileResult) but a real Excel workbook: a bold green
/// header row with the column names frozen in place, zebra-striped data rows, light grid
/// lines, and real currency/number/percent formatting instead of everything being text.
///
/// Built by hand against the OOXML spreadsheet format using only the .NET base class library
/// (System.IO.Compression) — no third-party package or network restore required.
/// </summary>
public sealed class XlsxFile
{
    private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>One block of header+rows. A file normally has exactly one; a report that
    /// stacks a second little table below the first (with its own, different columns)
    /// starts a new section instead, via <see cref="AddSection"/>.</summary>
    private sealed class Section
    {
        public required string[] Headers;
        public required XlsxColumn[] Columns;
        public readonly List<(object?[] Cells, bool IsTotal)> Rows = new();
    }

    private readonly string _currencySymbol;
    private readonly List<Section> _sections = new();
    private Section _current;

    public XlsxFile(string currencySymbol, string[] headers, params XlsxColumn[] columns)
    {
        _currencySymbol = currencySymbol;
        _current = new Section { Headers = headers, Columns = NormalizeColumns(headers.Length, columns) };
        _sections.Add(_current);
    }

    /// <summary>A plain, no-currency file (every column is Text/Integer/Percent — no money
    /// column at all), for the rare report that has nothing to show in its own currency.</summary>
    public XlsxFile(string[] headers, params XlsxColumn[] columns) : this("$", headers, columns) { }

    /// <summary>A normal data row, styled by its section's column kinds and banded
    /// light/white by its position within the section.</summary>
    public XlsxFile AddRow(params object?[] cells)
    {
        _current.Rows.Add((cells, IsTotal: false));
        return this;
    }

    /// <summary>A totals/summary row: bold, boxed in its own light border, and excluded
    /// from the odd/even banding so it visually stands apart from the data above it.</summary>
    public XlsxFile AddTotalRow(params object?[] cells)
    {
        _current.Rows.Add((cells, IsTotal: true));
        return this;
    }

    /// <summary>Starts a second table further down the same sheet, with its own header row
    /// and column kinds (used by the Income report, which stacks a small by-month trend
    /// table under the main by-source one).</summary>
    public XlsxFile AddSection(string[] headers, params XlsxColumn[] columns)
    {
        _current = new Section { Headers = headers, Columns = NormalizeColumns(headers.Length, columns) };
        _sections.Add(_current);
        return this;
    }

    private static XlsxColumn[] NormalizeColumns(int headerCount, XlsxColumn[] columns)
    {
        // Callers list a kind per real column; anything left unsaid defaults to Text.
        var result = new XlsxColumn[headerCount];
        Array.Copy(columns, result, Math.Min(columns.Length, headerCount));
        return result;
    }

    public FileContentResult ToFileResult(string fileName)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", ContentTypesXml());
            WriteEntry(zip, "_rels/.rels", PackageRelsXml());
            WriteEntry(zip, "xl/workbook.xml", WorkbookXml());
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
            WriteEntry(zip, "xl/styles.xml", StylesXml());
            WriteEntry(zip, "xl/worksheets/sheet1.xml", SheetXml());
        }

        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return new FileContentResult(ms.ToArray(), contentType) { FileDownloadName = fileName };
    }

    // ------------------------------------------------------------------ worksheet contents

    /// <summary>cellXfs indices — see StylesXml for what each one actually is.</summary>
    private static class Style
    {
        public const int Header = 1;
        public const int TextOdd = 2, TextEven = 3;
        public const int CurrencyOdd = 4, CurrencyEven = 5;
        public const int IntegerOdd = 6, IntegerEven = 7;
        public const int PercentOdd = 8, PercentEven = 9;
        public const int TotalText = 10, TotalCurrency = 11, TotalInteger = 12, TotalPercent = 13;
    }

    private string SheetXml()
    {
        var totalCols = _sections.Max(s => s.Headers.Length);
        var totalRows = _sections.Sum(s => 1 + s.Rows.Count) + (_sections.Count - 1); // +1 blank spacer between sections

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append($"<worksheet xmlns=\"{NsMain}\">");
        sb.Append($"<dimension ref=\"A1:{ColumnLetter(Math.Max(1, totalCols))}{Math.Max(1, totalRows)}\"/>");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<cols>");
        foreach (var width in ColumnWidths(totalCols))
        {
            sb.Append($"<col min=\"{width.Index}\" max=\"{width.Index}\" width=\"{width.Width.ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
        }
        sb.Append("</cols>");
        sb.Append("<sheetData>");

        var rowNum = 1;
        var firstSectionRange = string.Empty;
        for (var s = 0; s < _sections.Count; s++)
        {
            var section = _sections[s];
            if (s > 0)
            {
                sb.Append($"<row r=\"{rowNum}\"/>"); // blank spacer row between stacked tables
                rowNum++;
            }

            sb.Append($"<row r=\"{rowNum}\">");
            for (var c = 0; c < section.Headers.Length; c++)
            {
                AppendCell(sb, c, rowNum, section.Headers[c], Style.Header);
            }
            sb.Append("</row>");
            var headerRow = rowNum;
            rowNum++;

            var dataIndex = 0; // only data rows (not totals) advance the zebra banding
            foreach (var (cells, isTotal) in section.Rows)
            {
                sb.Append($"<row r=\"{rowNum}\">");
                var banded = !isTotal && dataIndex % 2 == 1;
                for (var c = 0; c < cells.Length && c < section.Columns.Length; c++)
                {
                    var style = StyleFor(section.Columns[c], isTotal, banded);
                    AppendCell(sb, c, rowNum, cells[c], style);
                }
                sb.Append("</row>");
                if (!isTotal) dataIndex++;
                rowNum++;
            }

            if (s == 0)
            {
                firstSectionRange = $"A{headerRow}:{ColumnLetter(section.Headers.Length)}{rowNum - 1}";
            }
        }

        sb.Append("</sheetData>");
        // An autofilter only makes sense over one clean table — skip it when a second
        // section is stacked below, so the dropdown arrows don't imply the whole sheet filters.
        if (_sections.Count == 1 && _sections[0].Rows.Count > 0)
        {
            sb.Append($"<autoFilter ref=\"{firstSectionRange}\"/>");
        }
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static int StyleFor(XlsxColumn column, bool isTotal, bool banded) => (column, isTotal) switch
    {
        (_, true) => column switch
        {
            XlsxColumn.Currency => Style.TotalCurrency,
            XlsxColumn.Integer => Style.TotalInteger,
            XlsxColumn.Percent => Style.TotalPercent,
            _ => Style.TotalText
        },
        (XlsxColumn.Currency, false) => banded ? Style.CurrencyEven : Style.CurrencyOdd,
        (XlsxColumn.Integer, false) => banded ? Style.IntegerEven : Style.IntegerOdd,
        (XlsxColumn.Percent, false) => banded ? Style.PercentEven : Style.PercentOdd,
        _ => banded ? Style.TextEven : Style.TextOdd
    };

    private static void AppendCell(StringBuilder sb, int col, int row, object? value, int style)
    {
        var @ref = ColumnLetter(col + 1) + row;
        var s = style == 0 ? "" : $" s=\"{style}\"";

        switch (value)
        {
            case null:
                if (style != 0) sb.Append($"<c r=\"{@ref}\"{s}/>"); // keep the banding/border even on a blank cell
                return;
            case string str when str.Length == 0:
                if (style != 0) sb.Append($"<c r=\"{@ref}\"{s}/>");
                return;
            case string str:
                sb.Append($"<c r=\"{@ref}\" t=\"inlineStr\"{s}><is><t xml:space=\"preserve\">{Escape(str)}</t></is></c>");
                return;
            case decimal or double or float or int or long or short:
                var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("0.################", CultureInfo.InvariantCulture);
                sb.Append($"<c r=\"{@ref}\"{s}><v>{number}</v></c>");
                return;
            case DateTime dt:
                sb.Append($"<c r=\"{@ref}\" t=\"inlineStr\"{s}><is><t>{dt:yyyy-MM-dd}</t></is></c>");
                return;
            default:
                sb.Append($"<c r=\"{@ref}\" t=\"inlineStr\"{s}><is><t xml:space=\"preserve\">{Escape(value.ToString() ?? string.Empty)}</t></is></c>");
                return;
        }
    }

    /// <summary>A reasonable column width guessed from the longest value seen in it (across
    /// every section), so the sheet doesn't open with every column squashed to Excel's
    /// cramped default.</summary>
    private IEnumerable<(int Index, double Width)> ColumnWidths(int totalCols)
    {
        for (var c = 0; c < totalCols; c++)
        {
            var longest = 0;
            foreach (var section in _sections)
            {
                if (c < section.Headers.Length) longest = Math.Max(longest, section.Headers[c].Length);
                foreach (var (cells, _) in section.Rows)
                {
                    if (c >= cells.Length || cells[c] is null) continue;
                    var text = cells[c] is DateTime dt ? dt.ToString("yyyy-MM-dd") : cells[c]!.ToString() ?? string.Empty;
                    longest = Math.Max(longest, text.Length);
                }
            }
            yield return (c + 1, Math.Clamp(longest + 2, 10, 42));
        }
    }

    private static string ColumnLetter(int oneBasedIndex)
    {
        var letters = string.Empty;
        while (oneBasedIndex > 0)
        {
            var remainder = (oneBasedIndex - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            oneBasedIndex = (oneBasedIndex - 1) / 26;
        }
        return letters;
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    /// <summary>Escapes a literal symbol for use inside a quoted section of an Excel number
    /// format code that itself sits inside an XML attribute, e.g. turns € into a safe literal
    /// and a literal double-quote into &amp;quot; so it can't break out of either quoting layer.</summary>
    private static string EscapeForQuotedFormat(string symbol) => Escape(symbol).Replace("\"", "&quot;");

    // ------------------------------------------------------------------ fixed package parts

    private static string ContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "</Types>";

    private static string PackageRelsXml() =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        $"<Relationships xmlns=\"{NsPkgRel}\">" +
        $"<Relationship Id=\"rId1\" Type=\"{NsRel}/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static string WorkbookXml() =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        $"<workbook xmlns=\"{NsMain}\" xmlns:r=\"{NsRel}\">" +
        "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
        "</workbook>";

    private static string WorkbookRelsXml() =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        $"<Relationships xmlns=\"{NsPkgRel}\">" +
        $"<Relationship Id=\"rId1\" Type=\"{NsRel}/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        $"<Relationship Id=\"rId2\" Type=\"{NsRel}/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    /// <summary>
    /// Fonts: 0 normal, 1 bold green (header), 2 bold black (totals).
    /// Fills: 0 none, 1 gray125 (conventional unused placeholder), 2 light green (header), 3 light gray (zebra).
    /// Borders: 0 none, 1 thin bottom (data rows), 2 thin top+bottom (header bottom edge / total rows).
    /// Number formats: 164 currency, 165 integer, 166 percent-as-literal.
    /// cellXfs: see the Style class for what each index means.
    /// </summary>
    private string StylesXml()
    {
        var currencyFmt = $"&quot;{EscapeForQuotedFormat(_currencySymbol)}&quot;#,##0.00;&quot;-{EscapeForQuotedFormat(_currencySymbol)}&quot;#,##0.00";

        return
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            $"<styleSheet xmlns=\"{NsMain}\">" +
            "<numFmts count=\"3\">" +
            $"<numFmt numFmtId=\"164\" formatCode=\"{currencyFmt}\"/>" +
            "<numFmt numFmtId=\"165\" formatCode=\"#,##0\"/>" +
            "<numFmt numFmtId=\"166\" formatCode=\"0.0&quot;%&quot;\"/>" +
            "</numFmts>" +
            "<fonts count=\"3\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><color rgb=\"FF065F46\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"4\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD1FAE5\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF4F6F9\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"3\">" +
            "<border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left/><right/><top/><bottom style=\"thin\"><color rgb=\"FFE2E8F0\"/></bottom><diagonal/></border>" +
            "<border><left/><right/><top style=\"thin\"><color rgb=\"FFCBD5E1\"/></top><bottom style=\"thin\"><color rgb=\"FFCBD5E1\"/></bottom><diagonal/></border>" +
            "</borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"14\">" +
            /* 0  default      */ "<xf numFmtId=\"0\"   fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            /* 1  header       */ "<xf numFmtId=\"0\"   fontId=\"1\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
            /* 2  text odd     */ "<xf numFmtId=\"0\"   fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +
            /* 3  text even    */ "<xf numFmtId=\"0\"   fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\"/>" +
            /* 4  currency odd */ "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            /* 5  currency even*/ "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
            /* 6  integer odd  */ "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            /* 7  integer even */ "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
            /* 8  percent odd  */ "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            /* 9  percent even */ "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
            /* 10 total text   */ "<xf numFmtId=\"0\"   fontId=\"2\" fillId=\"0\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\"/>" +
            /* 11 total currency*/ "<xf numFmtId=\"164\" fontId=\"2\" fillId=\"0\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            /* 12 total integer*/ "<xf numFmtId=\"165\" fontId=\"2\" fillId=\"0\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            /* 13 total percent*/ "<xf numFmtId=\"166\" fontId=\"2\" fillId=\"0\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";
    }

    private static void WriteEntry(ZipArchive zip, string path, string xml)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(xml);
        stream.Write(bytes, 0, bytes.Length);
    }
}
