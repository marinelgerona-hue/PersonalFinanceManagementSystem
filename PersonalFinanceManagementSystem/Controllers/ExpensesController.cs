using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Expense tracker — add, edit, delete and view expense records.</summary>
public class ExpensesController : AppControllerBase
{
    private readonly IExpenseService _expenses;
    private readonly IIncomeService _incomes;
    private readonly IBudgetService _budgets;

    public ExpensesController(
        IAuthService auth,
        IExpenseService expenses,
        IIncomeService incomes,
        IBudgetService budgets) : base(auth)
    {
        _expenses = expenses;
        _incomes = incomes;
        _budgets = budgets;
    }

    public IActionResult Index()
    {
        var now = DateTime.Now;
        var all = _expenses.GetAll(CurrentUser.Id);
        var vm = new ExpenseListViewModel
        {
            Items = all,
            Currency = CurrentUser.Currency,
            Total = all.Sum(e => e.Amount),
            MonthTotal = all.Where(e => e.Date.Year == now.Year && e.Date.Month == now.Month).Sum(e => e.Amount),
            ThisMonthCount = all.Count(e => e.Date.Year == now.Year && e.Date.Month == now.Month),

            // Cash balance = all income − all expenses; updates on every reload.
            // Budget deductions are tracked separately and never affect it —
            // income is only ever spent once, no matter how budgets allocate it.
            TotalIncome = _incomes.Total(CurrentUser.Id),

            // Budget name lookup for the “deducted from” column.
            BudgetNames = _budgets.GetAll(CurrentUser.Id)
                .ToDictionary(b => b.Id, b => BudgetDisplay(b))
        };
        ViewData["Title"] = "Expenses";
        ViewData["ActiveNav"] = "expenses";
        return View(vm);
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Expense";
        ViewData["ActiveNav"] = "expenses";

        return View("ExpenseForm", BuildExpenseForm(new ExpenseFormViewModel { Date = DateTime.Today }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ExpenseFormViewModel model)
    {
        ResolveCategory(model);
        var budget = ResolveBudget(CurrentUser.Id, model);
        ValidateIncomePool(model);

        if (!ModelState.IsValid)
        {
            RestoreCategoryForDisplay(model);
            ViewData["Title"] = "Add Expense";
            ViewData["ActiveNav"] = "expenses";
            return View("ExpenseForm", BuildExpenseForm(model));
        }

        _expenses.Add(new Expense
        {
            UserId = CurrentUser.Id,
            Category = model.Category,
            Amount = model.Amount,
            Date = model.Date,
            Note = model.Note ?? string.Empty,

            // The income pool the amount is deducted from.
            IncomePool = model.IncomePool,

            // Null means no explicit budget was picked — the amount still
            // counts against any matching budget whose period contains the date.
            BudgetId = budget?.Id
        });

        var remaining = _expenses.IncomePoolAvailable(
            CurrentUser.Id, model.IncomePool, model.Date.Date, null);
        TempData["Success"] = $"Expense of {Helpers.Format.Money(model.Amount, CurrentUser.Currency)} added" +
            $" — deducted from the {model.IncomePool} income pool ({Helpers.Format.Money(remaining, CurrentUser.Currency)} remaining)." +
            (budget is null ? string.Empty : $" Also deducted from the {budget.Category} budget.");
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var expense = _expenses.Get(CurrentUser.Id, id);
        if (expense is null) return NotFound();

        ViewData["Title"] = "Edit Expense";
        ViewData["ActiveNav"] = "expenses";
        // A previously saved custom category is not in the catalog, so the
        // form selects "Other" and prefills the custom field with its value.
        var isCustomCategory = !Catalog.ExpenseCategories.Contains(expense.Category);
        return View("ExpenseForm", BuildExpenseForm(new ExpenseFormViewModel
        {
            Id = expense.Id,
            Category = isCustomCategory ? "Other" : expense.Category,
            CustomCategory = isCustomCategory ? expense.Category : null,
            Amount = expense.Amount,
            Date = expense.Date,
            Note = expense.Note,
            BudgetId = expense.BudgetId,

            // Records saved before the pool feature existed default to Monthly.
            IncomePool = expense.IncomePool ?? IncomePeriod.Monthly
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ExpenseFormViewModel model)
    {
        ResolveCategory(model);
        var budget = ResolveBudget(CurrentUser.Id, model);
        ValidateIncomePool(model);

        if (!ModelState.IsValid)
        {
            RestoreCategoryForDisplay(model);
            ViewData["Title"] = "Edit Expense";
            ViewData["ActiveNav"] = "expenses";
            return View("ExpenseForm", BuildExpenseForm(model));
        }

        var updated = _expenses.Update(new Expense
        {
            Id = id,
            UserId = CurrentUser.Id,
            Category = model.Category,
            Amount = model.Amount,
            Date = model.Date,
            Note = model.Note ?? string.Empty,
            BudgetId = budget?.Id,

            IncomePool = model.IncomePool
        });

        TempData[updated ? "Success" : "Error"] = updated
            ? $"Expense updated — deducted from the {model.IncomePool} income pool."
            : "Expense record was not found.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ export

    /// <summary>Downloads every expense record as CSV.</summary>
    [HttpGet]
    public IActionResult Export()
    {
        var items = _expenses.GetAll(CurrentUser.Id);
        var csv = new Helpers.CsvFile("Date", "Category", "Amount", "Currency", "Note");
        foreach (var item in items)
        {
            csv.AddRow(item.Date, item.Category, item.Amount, CurrentUser.Currency, item.Note);
        }
        return csv.ToFileResult($"finapp-expenses-{DateTime.Today:yyyy-MM-dd}.csv");
    }

    // ------------------------------------------------------------ delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var deleted = _expenses.Delete(CurrentUser.Id, id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Expense record deleted — budget balances recalculated."
            : "Expense record was not found.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ budget helpers

    /// <summary>Populates the budget dropdown and the income-pool ledger for
    /// the expense form. The budget option matching the picked category +
    /// date is flagged so the view pre-selects the automatic deduction
    /// target; the ledger lets the browser show the live pool balance.</summary>
    private ExpenseFormViewModel BuildExpenseForm(ExpenseFormViewModel model)
    {
        model.Budgets = _budgets.GetOptionsForExpense(
            CurrentUser.Id, model.Category, model.Date);

        model.PoolLedger = _incomes.GetAll(CurrentUser.Id)
            .Select(i => new PoolLedgerEntry
            {
                Kind = "income",
                Period = i.Period,
                Date = i.Date,
                Amount = i.Amount
            })
            .Concat(_expenses.GetAll(CurrentUser.Id)
                .Where(e => e.IncomePool is not null)
                .Select(e => new PoolLedgerEntry
                {
                    Kind = "expense",
                    Period = e.IncomePool!.Value,
                    Date = e.Date,
                    Amount = e.Amount,
                    ExpenseId = e.Id
                }))
            .ToList();

        return model;
    }

    /// <summary>When Category is "Other", requires the custom category the
    /// user typed and stores it as the expense category. Custom text posted
    /// while a catalog category is picked (field is hidden) is ignored.</summary>
    private void ResolveCategory(ExpenseFormViewModel model)
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
    private static void RestoreCategoryForDisplay(ExpenseFormViewModel model)
    {
        if (model.CustomCategory is { } custom &&
            custom.Length > 0 &&
            !Catalog.ExpenseCategories.Contains(model.Category, StringComparer.OrdinalIgnoreCase) &&
            model.Category.Equals(custom, StringComparison.OrdinalIgnoreCase))
        {
            model.Category = "Other";
        }
    }

    /// <summary>Validates that the chosen income pool can cover the expense:
    /// income of the pool recorded on or before the expense date, minus
    /// expenses already drawn from the pool up to that date, must be at
    /// least the amount. An edit excludes its own old deduction.</summary>
    private void ValidateIncomePool(ExpenseFormViewModel model)
    {
        var asOf = model.Date.Date;
        var available = _expenses.IncomePoolAvailable(
            CurrentUser.Id, model.IncomePool, asOf, model.Id > 0 ? model.Id : null);

        if (model.Amount <= available) return;

        var shortfall = model.Amount - Math.Max(available, 0m);
        var opening = available <= 0m
            ? $"No unused {model.IncomePool} income has been recorded on or before {Helpers.Format.Date(asOf)} yet"
            : $"Only {Helpers.Format.Money(available, CurrentUser.Currency)} of {model.IncomePool} income " +
              $"recorded on or before {Helpers.Format.Date(asOf)} is available";

        ModelState.AddModelError(nameof(model.IncomePool),
            $"{opening}. This expense of {Helpers.Format.Money(model.Amount, CurrentUser.Currency)} " +
            $"would overdraw the {model.IncomePool} income pool by {Helpers.Format.Money(shortfall, CurrentUser.Currency)}. " +
            $"Choose a period with enough recorded income, or add {model.IncomePool} income dated on or before {Helpers.Format.Date(asOf)} first.");
    }

    /// <summary>Validates the budget the user picked (must exist on this
    /// account, match the expense category and cover the expense date).
    /// Returns the resolved budget, or null when a model error was added.</summary>
    private Budget? ResolveBudget(int userId, ExpenseFormViewModel model)
    {
        if (model.BudgetId is null)
        {
            return _budgets.MatchForExpense(userId, model.Category, model.Date) is { } auto
                ? auto // auto-match used for the confirmation message only
                : null;
        }

        var budget = _budgets.Get(userId, model.BudgetId.Value);

        if (budget is null)
        {
            ModelState.AddModelError(nameof(model.BudgetId),
                "The selected budget no longer exists. Please pick another one.");
            return null;
        }

        if (!budget.Category.Equals(model.Category, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.BudgetId),
                $"The selected budget is for {budget.Category}, not {model.Category}.");
            return null;
        }

        if (!budget.Covers(model.Date))
        {
            ModelState.AddModelError(nameof(model.BudgetId),
                $"The selected budget does not cover {model.Date:MMM d, yyyy}. Please pick a budget with a matching period.");
            return null;
        }

        return budget;
    }

    /// <summary>Short table label for a budget, e.g. “Groceries · monthly”.</summary>
    private static string BudgetDisplay(Budget budget) =>
        $"{budget.Category} · {budget.Period switch
        {
            BudgetPeriod.Daily => $"daily ({budget.PeriodStart:MMM d})",
            BudgetPeriod.Weekly => $"weekly ({budget.PeriodStart:MMM d}–{budget.PeriodEnd:MMM d})",
            BudgetPeriod.Yearly => $"yearly ({budget.Year})",
            _ => $"monthly ({budget.PeriodStart:MMM yyyy})"
        }}";
}
