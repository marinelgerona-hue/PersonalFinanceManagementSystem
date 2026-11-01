using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace PersonalFinanceManagementSystem.Helpers;

/// <summary>
/// Builds a simple, real multi-page PDF statement (title, summary lines, one
/// table with a header row and optional bold total row) by hand against the
/// raw PDF 1.4 file format — only the 14 standard fonts (Helvetica /
/// Helvetica-Bold), no embedded font file and no third-party package, the
/// same "no dependency" philosophy as <see cref="XlsxFile"/>.
///
/// Text is single-line with ellipsis truncation to the column's pixel width
/// (no word-wrap) — PDF has no built-in layout engine, so column fitting is
/// computed from a hardcoded Adobe AFM advance-width table for both fonts.
/// Only WinAnsi-safe characters are supported; anything else is mapped to a
/// close WinAnsi equivalent or '?'. Rows are paginated automatically, with
/// the table header repeated on every page.
/// </summary>
public sealed class PdfFile
{
    private const float PageWidth = 612f;
    private const float PageHeight = 792f;
    private const float Margin = 48f;
    private const float ContentWidth = PageWidth - 2 * Margin;
    private const float RowHeight = 16f;
    private const float HeaderRowHeight = 20f;

    private readonly string _title;
    private readonly string _subtitle;
    private readonly List<(string Label, string Value)> _summary = new();
    private string[] _headers = Array.Empty<string>();
    private float[] _colX = Array.Empty<float>();
    private float[] _colW = Array.Empty<float>();
    private bool[] _rightAlign = Array.Empty<bool>();
    private readonly List<(string[] Cells, bool IsTotal)> _rows = new();

    public PdfFile(string title, string subtitle = "")
    {
        _title = title;
        _subtitle = subtitle;
    }

    public PdfFile AddSummaryLine(string label, string value)
    {
        _summary.Add((label, value));
        return this;
    }

    /// <summary>Defines the table's columns. <paramref name="widthPercents"/> must sum to ~100
    /// and has one entry per header; <paramref name="rightAlign"/> flags numeric columns.</summary>
    public PdfFile AddTable(string[] headers, float[] widthPercents, bool[] rightAlign)
    {
        _headers = headers;
        _rightAlign = rightAlign;

        _colX = new float[headers.Length];
        _colW = new float[headers.Length];
        var x = Margin;
        for (var i = 0; i < headers.Length; i++)
        {
            _colX[i] = x;
            _colW[i] = ContentWidth * widthPercents[i] / 100f;
            x += _colW[i];
        }
        return this;
    }

    public PdfFile AddRow(params string[] cells)
    {
        _rows.Add((cells, false));
        return this;
    }

    public PdfFile AddTotalRow(params string[] cells)
    {
        _rows.Add((cells, true));
        return this;
    }

    public FileContentResult ToFileResult(string fileName)
    {
        var pages = Paginate();
        var bytes = BuildPdfBytes(pages);
        return new FileContentResult(bytes, "application/pdf") { FileDownloadName = fileName };
    }

    // ------------------------------------------------------------------ layout / pagination

    private List<List<string>> Paginate()
    {
        var pages = new List<List<string>>();
        var ops = new List<string>();
        pages.Add(ops);

        var y = PageHeight - Margin;

        y -= 16;
        ops.Add(Text(_title, Margin, y, bold: true, size: 16));

        if (_subtitle.Length > 0)
        {
            y -= 16;
            ops.Add(Text(_subtitle, Margin, y, bold: false, size: 9, gray: true));
        }

        foreach (var (label, value) in _summary)
        {
            y -= 14;
            ops.Add(Text(label + ":", Margin, y, bold: false, size: 9, gray: true));
            ops.Add(Text(value, Margin + 140, y, bold: true, size: 9));
        }

        y -= 20;

        void DrawTableHeader()
        {
            ops.Add(Rect(Margin, y - HeaderRowHeight + 4, ContentWidth, HeaderRowHeight, 0.82f, 0.98f, 0.9f));
            for (var i = 0; i < _headers.Length; i++)
            {
                var tx = _rightAlign[i] ? _colX[i] + _colW[i] - 4 - TextWidth(_headers[i], true, 9) : _colX[i] + 4;
                ops.Add(Text(Truncate(_headers[i], _colW[i] - 8, true, 9), tx, y - 14, bold: true, size: 9));
            }
            y -= HeaderRowHeight;
        }

        DrawTableHeader();

        for (var r = 0; r < _rows.Count; r++)
        {
            if (y - RowHeight < Margin)
            {
                ops = new List<string>();
                pages.Add(ops);
                y = PageHeight - Margin;
                DrawTableHeader();
            }

            var (cells, isTotal) = _rows[r];

            if (isTotal)
            {
                ops.Add(Line(Margin, y, Margin + ContentWidth, y, 0.4f));
            }
            else if (r % 2 == 1)
            {
                ops.Add(Rect(Margin, y - RowHeight + 3, ContentWidth, RowHeight, 0.96f, 0.97f, 0.98f));
            }

            for (var i = 0; i < cells.Length && i < _headers.Length; i++)
            {
                var cell = cells[i] ?? "";
                var w = TextWidth(cell, isTotal, 9);
                var tx = _rightAlign[i] ? _colX[i] + _colW[i] - 4 - Math.Min(w, _colW[i] - 8) : _colX[i] + 4;
                ops.Add(Text(Truncate(cell, _colW[i] - 8, isTotal, 9), tx, y - 11, bold: isTotal, size: 9));
            }

            y -= RowHeight;
        }

        // Footer page numbers, added once the total page count is known.
        for (var p = 0; p < pages.Count; p++)
        {
            pages[p].Add(Text($"Page {p + 1} of {pages.Count}", PageWidth - Margin - 70, Margin - 20, bold: false, size: 8, gray: true));
        }

        return pages;
    }

    // ------------------------------------------------------------------ content stream ops

    private static string Text(string text, float x, float y, bool bold, float size, bool gray = false)
    {
        var font = bold ? "/F2" : "/F1";
        var color = gray ? "0.45 0.45 0.45 rg" : "0 0 0 rg";
        return $"q {color} BT {font} {size.ToString(CultureInfo.InvariantCulture)} Tf 1 0 0 1 {Fmt(x)} {Fmt(y)} Tm ({Escape(text)}) Tj ET Q";
    }

    private static string Rect(float x, float y, float w, float h, float r, float g, float b)
        => $"q {Fmt(r)} {Fmt(g)} {Fmt(b)} rg {Fmt(x)} {Fmt(y)} {Fmt(w)} {Fmt(h)} re f Q";

    private static string Line(float x1, float y1, float x2, float y2, float gray)
        => $"q {Fmt(gray)} {Fmt(gray)} {Fmt(gray)} RG 0.75 w {Fmt(x1)} {Fmt(y1)} m {Fmt(x2)} {Fmt(y2)} l S Q";

    private static string Fmt(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ text: encoding, width, truncation

    /// <summary>Maps a Unicode character to a plain-ASCII equivalent (not the WinAnsi 0x80-0x9F
    /// "smart punctuation" byte range — several PDF text extractors, including poppler's
    /// pdftotext, don't resolve those bytes back to Unicode and render them as '?', even
    /// though on-screen viewers display them fine). Falls back to '?' for anything else.</summary>
    private static char MapChar(char c)
    {
        if (c is >= (char)32 and <= (char)126) return c;
        return c switch
        {
            '—' or '–' => '-',            // em dash, en dash
            '‘' or '’' => '\'',
            '“' or '”' => '"',
            '…' => '.',                    // ellipsis (single char; good enough for layout)
            '•' => '*',
            '\n' or '\r' or '\t' => ' ',
            _ => '?'
        };
    }

    private static string ToWinAnsi(string s) => new(s.Select(MapChar).ToArray());

    private static string Escape(string s)
    {
        var mapped = ToWinAnsi(s);
        var sb = new StringBuilder(mapped.Length);
        foreach (var c in mapped)
        {
            if (c is '(' or ')' or '\\') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Adobe AFM advance widths (1000 units/em) for Helvetica, ASCII 32-126.</summary>
    private static readonly int[] HelveticaWidths =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,
        556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };

    /// <summary>Adobe AFM advance widths (1000 units/em) for Helvetica-Bold, ASCII 32-126.</summary>
    private static readonly int[] HelveticaBoldWidths =
    {
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
        975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,333,333,333,584,556,
        333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,
        611,611,389,556,333,611,556,778,556,556,500,389,280,389,584
    };

    private static float CharWidth(char c, bool bold, float size)
    {
        var table = bold ? HelveticaBoldWidths : HelveticaWidths;
        var units = c is >= (char)32 and <= (char)126 ? table[c - 32] : 556;
        return units * size / 1000f;
    }

    private static float TextWidth(string s, bool bold, float size)
        => ToWinAnsi(s).Sum(c => CharWidth(c, bold, size));

    private static string Truncate(string s, float maxWidth, bool bold, float size)
    {
        var mapped = ToWinAnsi(s);
        if (TextWidth(mapped, bold, size) <= maxWidth) return mapped;

        var ellipsisWidth = TextWidth("...", bold, size);
        var sb = new StringBuilder();
        var w = 0f;
        foreach (var c in mapped)
        {
            var cw = CharWidth(c, bold, size);
            if (w + cw + ellipsisWidth > maxWidth) break;
            sb.Append(c);
            w += cw;
        }
        return sb.ToString().TrimEnd() + "...";
    }

    // ------------------------------------------------------------------ PDF file assembly

    private static byte[] BuildPdfBytes(List<List<string>> pages)
    {
        var objects = new List<string>();

        // Object numbers: 1=Catalog, 2=Pages, 3=Font Helvetica, 4=Font Helvetica-Bold,
        // then one Page + one Contents object per page, interleaved.
        const int catalogId = 1, pagesId = 2, fontRegularId = 3, fontBoldId = 4;
        var firstPageObjId = 5;

        var pageIds = new List<int>();
        var pageObjects = new List<string>();
        var nextId = firstPageObjId;

        foreach (var ops in pages)
        {
            var pageId = nextId++;
            var contentsId = nextId++;
            pageIds.Add(pageId);

            var stream = string.Join("\n", ops);
            var streamBytes = Encoding.Latin1.GetByteCount(stream);

            pageObjects.Add(
                $"{pageId} 0 obj\n<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {Fmt(PageWidth)} {Fmt(PageHeight)}] " +
                $"/Resources << /Font << /F1 {fontRegularId} 0 R /F2 {fontBoldId} 0 R >> >> /Contents {contentsId} 0 R >>\nendobj");

            pageObjects.Add(
                $"{contentsId} 0 obj\n<< /Length {streamBytes} >>\nstream\n{stream}\nendstream\nendobj");
        }

        objects.Add($"{catalogId} 0 obj\n<< /Type /Catalog /Pages {pagesId} 0 R >>\nendobj");
        objects.Add($"{pagesId} 0 obj\n<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageIds.Count} >>\nendobj");
        objects.Add($"{fontRegularId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj");
        objects.Add($"{fontBoldId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj");
        objects.AddRange(pageObjects);

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new int[objects.Count + 1]; // 1-indexed by object id

        foreach (var obj in objects)
        {
            // Every object string starts with "<id> 0 obj" so its object id is parsed back out.
            var id = int.Parse(obj[..obj.IndexOf(' ')], CultureInfo.InvariantCulture);
            offsets[id] = Encoding.Latin1.GetByteCount(sb.ToString());
            sb.Append(obj).Append('\n');
        }

        var xrefOffset = Encoding.Latin1.GetByteCount(sb.ToString());
        var totalObjects = objects.Count + 1;
        sb.Append($"xref\n0 {totalObjects}\n");
        sb.Append("0000000000 65535 f \n");
        for (var id = 1; id < totalObjects; id++)
        {
            sb.Append(offsets[id].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        sb.Append($"trailer\n<< /Size {totalObjects} /Root {catalogId} 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
