using System.Text;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Bank-statement import (.xlsx or .csv) — upload, preview (valid/duplicate/rejected), confirm.</summary>
public class ImportController : AppControllerBase
{
    private const long MaxFileLength = 2_000_000; // 2 MB
    private const string SessionKey = "Import.PendingCsv";

    private readonly IIncomeService _incomes;
    private readonly IExpenseService _expenses;

    public ImportController(IAuthService auth, IIncomeService incomes, IExpenseService expenses) : base(auth)
    {
        _incomes = incomes;
        _expenses = expenses;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Import";
        ViewData["ActiveNav"] = "import";
        return View();
    }

    /// <summary>Downloads the template as a real .xlsx workbook. Deliberately filled with
    /// placeholder text ("replace with your own transaction") so importing it unedited is
    /// rejected by the dummy-data check in <see cref="CsvImporter"/> — it teaches the format
    /// without being importable as real data by accident. The same file re-saved after
    /// editing in Excel (or a hand-written .csv in the same format) both import fine.</summary>
    [HttpGet]
    public IActionResult Sample()
    {
        var xlsx = new XlsxFile(CsvImporter.ExpectedHeader,
            XlsxColumn.Text, XlsxColumn.Text, XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Text);
        xlsx.AddRow("2026-10-01", "Expense", "Groceries", 84.20m, "Sample row — replace with your own transaction");
        xlsx.AddRow("2026-10-02", "Income", "Salary", 5450.00m, "Sample row — replace with your own transaction");
        xlsx.AddRow("2026-10-03", "Expense", "Dining", 32.50m, "Sample row — replace with your own transaction");
        return xlsx.ToFileResult("import-template.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxFileLength + 65_000)]
    public IActionResult Preview(IFormFile? file)
    {
        ViewData["Title"] = "Import";
        ViewData["ActiveNav"] = "import";

        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a file first.";
            return RedirectToAction(nameof(Index));
        }
        if (file.Length > MaxFileLength)
        {
            TempData["Error"] = "File is too large. Please choose a file under 2 MB.";
            return RedirectToAction(nameof(Index));
        }

        using var ms = new MemoryStream();
        file.CopyTo(ms);
        var bytes = ms.ToArray();
        HttpContext.Session.Set(SessionKey, bytes);

        var result = ParseAgainstExisting(bytes);
        return View(new ImportPreviewViewModel { Result = result, Currency = CurrentUser.Currency });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Confirm()
    {
        var bytes = HttpContext.Session.Get(SessionKey);
        if (bytes is null)
        {
            TempData["Error"] = "Nothing to import — please upload the file again.";
            return RedirectToAction(nameof(Index));
        }

        // Re-parsed from the stored bytes rather than trusting anything posted by the
        // browser, so the rows actually imported are exactly what was previewed.
        var result = ParseAgainstExisting(bytes);
        HttpContext.Session.Remove(SessionKey);

        if (result.FileRejectedReason is not null)
        {
            TempData["Error"] = result.FileRejectedReason;
            return RedirectToAction(nameof(Index));
        }

        _incomes.AddRange(result.Valid
            .Where(r => r.Type == RecurringTransactionType.Income)
            .Select(r => new Income { UserId = CurrentUser.Id, Source = r.Label, Amount = r.Amount, Date = r.Date, Note = r.Note }));

        _expenses.AddRange(result.Valid
            .Where(r => r.Type == RecurringTransactionType.Expense)
            .Select(r => new Expense { UserId = CurrentUser.Id, Category = r.Label, Amount = r.Amount, Date = r.Date, Note = r.Note }));

        TempData["Success"] = $"Imported {result.Valid.Count} transaction{(result.Valid.Count == 1 ? "" : "s")} — " +
            $"skipped {result.Duplicates.Count} duplicate{(result.Duplicates.Count == 1 ? "" : "s")}, " +
            $"{result.Rejected.Count} row{(result.Rejected.Count == 1 ? "" : "s")} rejected.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>.xlsx files are zip archives, which always start with the two-byte "PK"
    /// signature — everything else is treated as CSV text. Sniffing the bytes (rather than
    /// trusting the uploaded filename/content-type) also lets Confirm re-detect the same way
    /// from the bytes alone, since it has no IFormFile to read an extension from.</summary>
    private static bool IsXlsx(byte[] bytes) => bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B;

    private ImportResult ParseAgainstExisting(byte[] bytes)
    {
        var existing = _incomes.GetAll(CurrentUser.Id).Select(i => (i.Date, i.Amount, i.Source, IsIncome: true))
            .Concat(_expenses.GetAll(CurrentUser.Id).Select(e => (e.Date, e.Amount, e.Category, IsIncome: false)))
            .Select(e => (e.Date, e.Amount, Label: e.Item3, e.IsIncome))
            .ToList();

        return IsXlsx(bytes)
            ? CsvImporter.ParseXlsx(bytes, existing)
            : CsvImporter.Parse(Encoding.UTF8.GetString(bytes), existing);
    }
}
