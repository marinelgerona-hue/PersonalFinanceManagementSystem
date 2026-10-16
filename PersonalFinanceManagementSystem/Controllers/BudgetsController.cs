using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Budget manager — per-category limits over a budget period
/// (daily, weekly, monthly or yearly) with live spent/remaining.</summary>
public class BudgetsController : AppControllerBase
{
    private readonly IBudgetService _budgets;

    public BudgetsController(IAuthService auth, IBudgetService budgets) : base(auth)
        => _budgets = budgets;

    /// <summary>Sortable columns of the budget table.</summary>
    private static readonly Dictionary<string, Func<BudgetStatus, object?>> BudgetSorts = new()
    {
        ["category"] = b => b.Budget.Category,
        ["limit"] = b => b.Budget.Limit,
        ["spent"] = b => b.Spent,
        ["remaining"] = b => b.Remaining,
        ["used"] = b => b.PercentUsed
    };

    public IActionResult Index(int? year, int? month, ListQuery query)
    {
        var now = DateTime.Now;
        var items = _budgets.GetStatusForMonth(CurrentUser.Id, year ?? now.Year, month ?? now.Month);
        query.Normalize(BudgetSorts.Keys, defaultSort: "used", defaultDir: "desc");
        var vm = new BudgetListViewModel
        {
            Year = year ?? now.Year,
            Month = month ?? now.Month,
            Currency = CurrentUser.Currency,
            Items = items,
            Query = query,
            Page = items.ToPaged(query, BudgetSorts,
                (b, term) => ListQueryExtensions.AnyContains(term, b.Budget.Category, b.Budget.Period.ToString()))
        };
        ViewData["Title"] = "Budgets";
        ViewData["ActiveNav"] = "budgets";
        return View(vm);
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create()
    {
        var now = DateTime.Now;
        ViewData["Title"] = "Add Budget";
        ViewData["ActiveNav"] = "budgets";
        return View("BudgetForm", new BudgetFormViewModel
        {
            Year = now.Year,
            Month = now.Month,
            Period = BudgetPeriod.Monthly,
            Day = 1,
            AnchorDate = now.Date
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(BudgetFormViewModel model)
    {
        ApplyPeriod(model);
        ResolveCategory(model);
        if (!ModelState.IsValid)
        {
            RestoreCategoryForDisplay(model);
            ViewData["Title"] = "Add Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        var error = _budgets.Add(BuildBudget(model));

        if (error.Length > 0)
        {
            ModelState.AddModelError(nameof(model.Category), error);
            RestoreCategoryForDisplay(model);
            ViewData["Title"] = "Add Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        TempData["Success"] = $"{model.Period} budget for {model.Category} added.";
        return RedirectToAction(nameof(Index), new { year = model.Year, month = model.Month });
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var budget = _budgets.Get(CurrentUser.Id, id);
        if (budget is null) return NotFound();

        ViewData["Title"] = "Edit Budget";
        ViewData["ActiveNav"] = "budgets";
        // A previously saved custom category is not in the catalog, so the
        // form selects "Other" and prefills the custom field with its value.
        var isCustomCategory = !Catalog.ExpenseCategories.Contains(budget.Category);
        return View("BudgetForm", new BudgetFormViewModel
        {
            Id = budget.Id,
            Category = isCustomCategory ? "Other" : budget.Category,
            CustomCategory = isCustomCategory ? budget.Category : null,
            Limit = budget.Limit,
            Year = budget.Year,
            Month = budget.Month,
            Period = budget.Period,
            Day = budget.Day,
            AnchorDate = budget.Period is BudgetPeriod.Daily or BudgetPeriod.Weekly
                ? budget.PeriodStart
                : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, BudgetFormViewModel model)
    {
        ApplyPeriod(model);
        ResolveCategory(model);
        if (!ModelState.IsValid)
        {
            RestoreCategoryForDisplay(model);
            ViewData["Title"] = "Edit Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        var error = _budgets.Update(BuildBudget(model, id));

        if (error.Length > 0)
        {
            ModelState.AddModelError(nameof(model.Category), error);
            RestoreCategoryForDisplay(model);
            ViewData["Title"] = "Edit Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        TempData["Success"] = "Budget updated.";
        return RedirectToAction(nameof(Index), new { year = model.Year, month = model.Month });
    }

    // ------------------------------------------------------------ delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var deleted = _budgets.Delete(CurrentUser.Id, id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Budget deleted — its expenses recalculate against any matching budget."
            : "Budget was not found.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ internals

    /// <summary>When Category is "Other", requires the custom category the
    /// user typed and stores it as the budget category. Custom text posted
    /// while a catalog category is picked (field is hidden) is ignored.</summary>
    private void ResolveCategory(BudgetFormViewModel model)
    {
        if (model.Category != "Other") return;

        var custom = model.CustomCategory?.Trim() ?? string.Empty;

        if (custom.Length == 0)
        {
            ModelState.AddModelError(nameof(model.CustomCategory),
                "Please enter your category.");
            return;
        }

        model.Category = custom;
    }

    /// <summary>For validation-failure re-renders after a successful "Other"
    /// mapping: re-selects "Other" and keeps the typed value so the user
    /// still sees the custom category that was applied.</summary>
    private static void RestoreCategoryForDisplay(BudgetFormViewModel model)
    {
        if (model.CustomCategory is { } custom &&
            custom.Length > 0 &&
            !Catalog.ExpenseCategories.Contains(model.Category, StringComparer.OrdinalIgnoreCase) &&
            model.Category.Equals(custom, StringComparison.OrdinalIgnoreCase))
        {
            model.Category = "Other";
        }
    }

    /// <summary>For daily and weekly budgets, folds the picked anchor date
    /// into Year/Month/Day, and requires that a date was picked.</summary>
    private void ApplyPeriod(BudgetFormViewModel model)
    {
        if (model.Period is BudgetPeriod.Daily or BudgetPeriod.Weekly)
        {
            if (model.AnchorDate is not { } anchor)
            {
                ModelState.AddModelError(nameof(model.AnchorDate),
                    "Please pick the date the budget starts on.");
                return;
            }

            model.Year = anchor.Year;
            model.Month = anchor.Month;
            model.Day = anchor.Day;
        }
    }

    private Budget BuildBudget(BudgetFormViewModel model, int id = 0) => new()
    {
        Id = id,
        UserId = CurrentUser.Id,
        Category = model.Category,
        Limit = model.Limit,
        Year = model.Year,
        Month = model.Month,
        Period = model.Period,
        Day = model.Day
    };
}
