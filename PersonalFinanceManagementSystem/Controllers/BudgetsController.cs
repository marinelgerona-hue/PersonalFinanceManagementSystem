using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Budget manager — per-category limits over a Start Date to End
/// Date schedule, with live spent/remaining.</summary>
public class BudgetsController : AppControllerBase
{
    private readonly IBudgetService _budgets;

    private readonly ICategoryService _categories;

    public BudgetsController(IAuthService auth, IBudgetService budgets, ICategoryService categories) : base(auth)
    {
        _budgets = budgets;
        _categories = categories;
    }

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
                (b, term) => ListQueryExtensions.AnyContains(term, b.Budget.Category,
                    b.Budget.StartDate.ToString("MMM d, yyyy"),
                    b.Budget.EndDate.ToString("MMM d, yyyy")))
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
            StartDate = now.Date,
            EndDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)).Date
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(BudgetFormViewModel model)
    {
        ResolveCategory(model);
        ValidateSchedule(model);
        if (!ModelState.IsValid)
        {
            RestoreCategoryForDisplay(model, _categories.GetActive(CategoryType.Expense));
            ViewBag.Categories = _categories.GetActive(CategoryType.Expense);
            ViewData["Title"] = "Add Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        var error = _budgets.Add(BuildBudget(model));

        if (error.Length > 0)
        {
            ModelState.AddModelError(nameof(model.Category), error);
            RestoreCategoryForDisplay(model, _categories.GetActive(CategoryType.Expense));
            ViewBag.Categories = _categories.GetActive(CategoryType.Expense);
            ViewData["Title"] = "Add Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        TempData["Success"] = $"Budget for {model.Category} ({model.StartDate:MMM d, yyyy} – {model.EndDate:MMM d, yyyy}) added.";
        return RedirectToAction(nameof(Index), new { year = model.StartDate.Year, month = model.StartDate.Month });
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var budget = _budgets.Get(CurrentUser.Id, id);
        if (budget is null) return NotFound();

        ViewData["Title"] = "Edit Budget";
        ViewData["ActiveNav"] = "budgets";
        // Admin-managed category catalog drives the dropdown; falls back to the built-in list.
        var categories = _categories.GetActive(CategoryType.Expense);
        ViewBag.Categories = categories;

        // A previously saved custom category is not in the catalog, so the
        // form selects "Other" and prefills the custom field with its value.
        var isCustomCategory = !categories.Contains(budget.Category);
        return View("BudgetForm", new BudgetFormViewModel
        {
            Id = budget.Id,
            Category = isCustomCategory ? "Other" : budget.Category,
            CustomCategory = isCustomCategory ? budget.Category : null,
            Limit = budget.Limit,
            StartDate = budget.StartDate.Date,
            EndDate = budget.EndDate.Date
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, BudgetFormViewModel model)
    {
        ResolveCategory(model);
        ValidateSchedule(model);
        if (!ModelState.IsValid)
        {
            RestoreCategoryForDisplay(model, _categories.GetActive(CategoryType.Expense));
            ViewBag.Categories = _categories.GetActive(CategoryType.Expense);
            ViewData["Title"] = "Edit Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        var error = _budgets.Update(BuildBudget(model, id));

        if (error.Length > 0)
        {
            ModelState.AddModelError(nameof(model.Category), error);
            RestoreCategoryForDisplay(model, _categories.GetActive(CategoryType.Expense));
            ViewBag.Categories = _categories.GetActive(CategoryType.Expense);
            ViewData["Title"] = "Edit Budget";
            ViewData["ActiveNav"] = "budgets";
            return View("BudgetForm", model);
        }

        TempData["Success"] = "Budget updated.";
        return RedirectToAction(nameof(Index), new { year = model.StartDate.Year, month = model.StartDate.Month });
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
    private static void RestoreCategoryForDisplay(BudgetFormViewModel model, IReadOnlyList<string> catalog)
    {
        if (model.CustomCategory is { } custom &&
            custom.Length > 0 &&
            !catalog.Contains(model.Category, StringComparer.OrdinalIgnoreCase) &&
            model.Category.Equals(custom, StringComparison.OrdinalIgnoreCase))
        {
            model.Category = "Other";
        }
    }

    /// <summary>The budget schedule must run forward: End Date cannot fall
    /// before the Start Date.</summary>
    private void ValidateSchedule(BudgetFormViewModel model)
    {
        if (model.EndDate.Date < model.StartDate.Date)
        {
            ModelState.AddModelError(nameof(model.EndDate),
                "End date must be on or after the start date.");
        }
    }

    private Budget BuildBudget(BudgetFormViewModel model, int id = 0) => new()
    {
        Id = id,
        UserId = CurrentUser.Id,
        Category = model.Category,
        Limit = model.Limit,
        StartDate = model.StartDate.Date,
        EndDate = model.EndDate.Date
    };
}
