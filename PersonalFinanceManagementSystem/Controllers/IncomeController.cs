using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Income management — add, edit, delete and view income records.</summary>
public class IncomeController : AppControllerBase
{
    private readonly IIncomeService _incomes;
    private readonly ICategoryService _categories;
    private readonly IActivityLogger _activity;

    public IncomeController(
        IAuthService auth,
        IIncomeService incomes,
        ICategoryService categories,
        IActivityLogger activity) : base(auth)
    {
        _incomes = incomes;
        _categories = categories;
        _activity = activity;
    }

    /// <summary>Audits income changes made while signed in as an administrator.
    /// Regular users' own income edits are not system activity.</summary>
    private void LogAdminChange(string action, string detail)
    {
        if (CurrentUser.Role == UserRoles.Admin)
            _activity.Log(HttpContext, CurrentUser.Id, CurrentUser.FullName, CurrentUser.Role, action, detail);
    }

    /// <summary>Sortable columns of the income table.</summary>
    private static readonly Dictionary<string, Func<Income, object?>> IncomeSorts = new()
    {
        ["date"] = i => i.Date,
        ["source"] = i => i.Source,
        ["period"] = i => i.Period,
        ["note"] = i => i.Note,
        ["amount"] = i => i.Amount
    };

    public IActionResult Index(IncomePeriod? period, ListQuery query)
    {
        var now = DateTime.Now;
        var all = _incomes.GetAll(CurrentUser.Id);

        // When a period filter is chosen, the table only shows that kind of income.
        var filtered = period is null
            ? all
            : all.Where(i => i.Period == period).ToList();

        query.Normalize(IncomeSorts.Keys, defaultSort: "date");
        var vm = new IncomeListViewModel
        {
            Query = query,
            Page = filtered.ToPaged(query, IncomeSorts,
                (i, term) => ListQueryExtensions.AnyContains(term, i.Source, i.Note, i.Period.ToString())),
            Currency = CurrentUser.Currency,

            // All-time income: the total of all unique records, counted once.
            Total = all.Sum(i => i.Amount),

            // “This month” counts only Monthly-period income received in the
            // current calendar month. Weekly, daily and yearly income stay
            // exclusively in their own period cards above — no record is
            // counted in more than one summary.
            MonthTotal = all
                .Where(i => i.Period == IncomePeriod.Monthly &&
                            i.Date.Year == now.Year &&
                            i.Date.Month == now.Month)
                .Sum(i => i.Amount),

            ThisMonthCount = all
                .Count(i => i.Period == IncomePeriod.Monthly &&
                            i.Date.Year == now.Year &&
                            i.Date.Month == now.Month),

            PeriodFilter = period,
            PeriodBreakdown = _incomes.TotalsByPeriod(CurrentUser.Id)
        };
        ViewData["Title"] = "Income";
        ViewData["ActiveNav"] = "income";
        return View(vm);
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Income";
        ViewData["ActiveNav"] = "income";
        ViewBag.Sources = _categories.GetActive(CategoryType.Income);
        return View("IncomeForm", new IncomeFormViewModel { Date = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(IncomeFormViewModel model)
    {
        ResolveSource(model);
        if (!ModelState.IsValid)
        {
            RestoreSourceForDisplay(model, _categories.GetActive(CategoryType.Income));
            ViewBag.Sources = _categories.GetActive(CategoryType.Income);
            ViewData["Title"] = "Add Income";
            ViewData["ActiveNav"] = "income";
            return View("IncomeForm", model);
        }

        _incomes.Add(new Income
        {
            UserId = CurrentUser.Id,
            Source = model.Source,
            Amount = model.Amount,
            Period = model.Period,
            Date = model.Date,
            Note = model.Note ?? string.Empty
        });

        LogAdminChange("IncomeCreated",
            $"Added income of {Helpers.Format.Money(model.Amount, CurrentUser.Currency)} " +
            $"from {model.Source} dated {model.Date:MMM d, yyyy}.");

        TempData["Success"] = $"Income of {Helpers.Format.Money(model.Amount, CurrentUser.Currency)} added.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ custom source

    /// <summary>When Source is "Other", requires the custom source the user
    /// typed and stores it as the income source. Custom text posted while a
    /// catalog source is picked (field is hidden) is ignored.</summary>
    private void ResolveSource(IncomeFormViewModel model)
    {
        if (model.Source != "Other") return;

        var custom = model.CustomSource?.Trim() ?? string.Empty;

        if (custom.Length == 0)
        {
            ModelState.AddModelError(nameof(model.CustomSource),
                "Please enter your income source.");
            return;
        }

        model.Source = custom;
    }

    /// <summary>For validation-failure re-renders after a successful "Other"
    /// mapping: re-selects "Other" and keeps the typed value so the user
    /// still sees the custom source that was applied.</summary>
    private static void RestoreSourceForDisplay(IncomeFormViewModel model, IReadOnlyList<string> catalog)
    {
        if (model.CustomSource is { } custom &&
            custom.Length > 0 &&
            !catalog.Contains(model.Source, StringComparer.OrdinalIgnoreCase) &&
            model.Source.Equals(custom, StringComparison.OrdinalIgnoreCase))
        {
            model.Source = "Other";
        }
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var income = _incomes.Get(CurrentUser.Id, id);
        if (income is null) return NotFound();

        ViewData["Title"] = "Edit Income";
        ViewData["ActiveNav"] = "income";
        ViewBag.Sources = _categories.GetActive(CategoryType.Income);

        // A previously saved custom source is not in the catalog, so the
        // form selects "Other" and prefills the custom field with its value.
        var isCustomSource = !ViewBag.Sources.Contains(income.Source);
        return View("IncomeForm", new IncomeFormViewModel
        {
            Id = income.Id,
            Source = isCustomSource ? "Other" : income.Source,
            CustomSource = isCustomSource ? income.Source : null,
            Amount = income.Amount,
            Period = income.Period,
            Date = income.Date,
            Note = income.Note
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, IncomeFormViewModel model)
    {
        ResolveSource(model);
        if (!ModelState.IsValid)
        {
            RestoreSourceForDisplay(model, _categories.GetActive(CategoryType.Income));
            ViewBag.Sources = _categories.GetActive(CategoryType.Income);
            ViewData["Title"] = "Edit Income";
            ViewData["ActiveNav"] = "income";
            return View("IncomeForm", model);
        }

        var updated = _incomes.Update(new Income
        {
            Id = id,
            UserId = CurrentUser.Id,
            Source = model.Source,
            Amount = model.Amount,
            Period = model.Period,
            Date = model.Date,
            Note = model.Note ?? string.Empty
        });

        if (updated)
        {
            LogAdminChange("IncomeUpdated",
                $"Updated income record #{id} to {Helpers.Format.Money(model.Amount, CurrentUser.Currency)} " +
                $"from {model.Source} dated {model.Date:MMM d, yyyy}.");
        }

        TempData[updated ? "Success" : "Error"] = updated
            ? "Income updated."
            : "Income record was not found.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ export

    /// <summary>Downloads every income record as an Excel workbook.</summary>
    [HttpGet]
    public IActionResult Export()
    {
        var items = _incomes.GetAll(CurrentUser.Id);
        var csv = new Helpers.XlsxFile(Helpers.Format.Symbol(CurrentUser.Currency),
            new[] { "Date", "Source", "Period", "Amount", "Currency", "Note" },
            Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text,
            Helpers.XlsxColumn.Currency, Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text);
        foreach (var item in items)
        {
            csv.AddRow(item.Date, item.Source, item.Period.ToString(), item.Amount, CurrentUser.Currency, item.Note);
        }
        return csv.ToFileResult($"finapp-income-{DateTime.Today:yyyy-MM-dd}.xlsx");
    }

    // ------------------------------------------------------------ delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var deleted = _incomes.Delete(CurrentUser.Id, id);
        if (deleted)
            LogAdminChange("IncomeDeleted", $"Deleted income record #{id}.");

        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Income record deleted."
            : "Income record was not found.";
        return RedirectToAction(nameof(Index));
    }
}
