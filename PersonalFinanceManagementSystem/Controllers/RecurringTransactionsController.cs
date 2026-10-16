using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Recurring bills/income — define once, occurrences generate automatically.</summary>
public class RecurringTransactionsController : AppControllerBase
{
    private readonly IRecurringTransactionService _recurring;
    private readonly IBudgetService _budgets;

    public RecurringTransactionsController(
        IAuthService auth,
        IRecurringTransactionService recurring,
        IBudgetService budgets) : base(auth)
    {
        _recurring = recurring;
        _budgets = budgets;
    }

    private static readonly Dictionary<string, Func<RecurringTransaction, object?>> Sorts = new()
    {
        ["label"] = r => r.Label,
        ["type"] = r => r.Type,
        ["amount"] = r => r.Amount,
        ["frequency"] = r => r.Frequency,
        ["nextduedate"] = r => r.NextDueDate,
        ["status"] = r => !r.IsActive ? 2 : r.NextDueDate.Date < DateTime.Today ? 0 : 1
    };

    public IActionResult Index(ListQuery query)
    {
        var all = _recurring.GetAll(CurrentUser.Id);
        var budgetNames = _budgets.GetAll(CurrentUser.Id).ToDictionary(b => b.Id, b => b.Category);

        query.Normalize(Sorts.Keys, defaultSort: "nextduedate", defaultDir: "asc");

        var vm = new RecurringTransactionListViewModel
        {
            Query = query,
            Page = all.ToPaged(query, Sorts,
                (r, term) => ListQueryExtensions.AnyContains(term, r.Label, r.Note)),
            Currency = CurrentUser.Currency,
            ActiveCount = all.Count(r => r.IsActive),
            DueSoonCount = all.Count(r => r.IsActive && r.NextDueDate.Date >= DateTime.Today && r.NextDueDate.Date <= DateTime.Today.AddDays(7)),
            OverdueCount = all.Count(r => r.IsActive && r.NextDueDate.Date < DateTime.Today),
            MonthlyTotal = all.Where(r => r.IsActive && r.Frequency == RecurrenceFrequency.Monthly).Sum(r => r.Amount),
            BudgetNames = budgetNames
        };
        ViewData["Title"] = "Recurring";
        ViewData["ActiveNav"] = "recurring";
        return View(vm);
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Recurring Transaction";
        ViewData["ActiveNav"] = "recurring";
        return View("RecurringTransactionForm", BuildForm(new RecurringTransactionFormViewModel { StartDate = DateTime.Today }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(RecurringTransactionFormViewModel model)
    {
        ResolveLabel(model);
        var budget = ResolveBudget(model);

        if (model.EndDate is not null && model.EndDate.Value.Date < model.StartDate.Date)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after the start date.");
        }

        if (!ModelState.IsValid)
        {
            RestoreLabelForDisplay(model);
            ViewData["Title"] = "Add Recurring Transaction";
            ViewData["ActiveNav"] = "recurring";
            return View("RecurringTransactionForm", BuildForm(model));
        }

        _recurring.Add(new RecurringTransaction
        {
            UserId = CurrentUser.Id,
            Type = model.Type,
            Label = model.Label,
            Amount = model.Amount,
            Frequency = model.Frequency,
            StartDate = model.StartDate,
            NextDueDate = model.StartDate,
            EndDate = model.EndDate,
            IsActive = model.IsActive,
            Note = model.Note ?? string.Empty,
            IncomePool = model.Type == RecurringTransactionType.Expense ? model.IncomePool : null,
            BudgetId = model.Type == RecurringTransactionType.Expense ? budget?.Id : null
        });

        TempData["Success"] = $"{model.Label} will repeat {model.Frequency.ToString().ToLowerInvariant()}, starting {Helpers.Format.Date(model.StartDate)}.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var item = _recurring.Get(CurrentUser.Id, id);
        if (item is null) return NotFound();

        ViewData["Title"] = "Edit Recurring Transaction";
        ViewData["ActiveNav"] = "recurring";
        var catalog = item.Type == RecurringTransactionType.Expense ? Catalog.ExpenseCategories : Catalog.IncomeSources;
        var isCustomLabel = !catalog.Contains(item.Label);

        return View("RecurringTransactionForm", BuildForm(new RecurringTransactionFormViewModel
        {
            Id = item.Id,
            Type = item.Type,
            Label = isCustomLabel ? "Other" : item.Label,
            CustomLabel = isCustomLabel ? item.Label : null,
            Amount = item.Amount,
            Frequency = item.Frequency,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            IsActive = item.IsActive,
            Note = item.Note,
            BudgetId = item.BudgetId,
            IncomePool = item.IncomePool ?? IncomePeriod.Monthly
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, RecurringTransactionFormViewModel model)
    {
        ResolveLabel(model);
        var budget = ResolveBudget(model);

        if (model.EndDate is not null && model.EndDate.Value.Date < model.StartDate.Date)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after the start date.");
        }

        if (!ModelState.IsValid)
        {
            RestoreLabelForDisplay(model);
            ViewData["Title"] = "Edit Recurring Transaction";
            ViewData["ActiveNav"] = "recurring";
            return View("RecurringTransactionForm", BuildForm(model));
        }

        var existing = _recurring.Get(CurrentUser.Id, id);
        if (existing is null) return NotFound();

        var updated = _recurring.Update(new RecurringTransaction
        {
            Id = id,
            UserId = CurrentUser.Id,
            Type = model.Type,
            Label = model.Label,
            Amount = model.Amount,
            Frequency = model.Frequency,
            StartDate = model.StartDate,
            // Changing Frequency/StartDate only affects future occurrences —
            // the schedule cursor itself is left untouched by an edit.
            NextDueDate = existing.NextDueDate,
            EndDate = model.EndDate,
            IsActive = model.IsActive,
            Note = model.Note ?? string.Empty,
            IncomePool = model.Type == RecurringTransactionType.Expense ? model.IncomePool : null,
            BudgetId = model.Type == RecurringTransactionType.Expense ? budget?.Id : null
        });

        TempData[updated ? "Success" : "Error"] = updated
            ? "Recurring transaction updated."
            : "Recurring transaction was not found.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ skip / delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Skip(int id, string? returnUrl)
    {
        var skipped = _recurring.SkipNext(CurrentUser.Id, id);
        TempData[skipped ? "Success" : "Error"] = skipped
            ? "Next occurrence skipped — nothing was recorded."
            : "Recurring transaction was not found.";
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id, string? returnUrl)
    {
        var deleted = _recurring.Delete(CurrentUser.Id, id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Recurring transaction deleted. Records it already generated are kept."
            : "Recurring transaction was not found.";
        return RedirectToReturnUrl(returnUrl);
    }

    // ------------------------------------------------------------ details

    [HttpGet]
    public IActionResult Details(int id)
    {
        var item = _recurring.Get(CurrentUser.Id, id);
        if (item is null) return NotFound();

        ViewData["Title"] = item.Label;
        ViewData["ActiveNav"] = "recurring";
        return View(new RecurringTransactionDetailsViewModel
        {
            Item = item,
            Currency = CurrentUser.Currency,
            History = _recurring.GetHistory(CurrentUser.Id, id)
        });
    }

    // ------------------------------------------------------------ export

    [HttpGet]
    public IActionResult Export()
    {
        var items = _recurring.GetAll(CurrentUser.Id);
        var xlsx = new Helpers.XlsxFile(Helpers.Format.Symbol(CurrentUser.Currency),
            new[] { "Type", "Label", "Amount", "Frequency", "Next Due", "End Date", "Status", "Note" },
            Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Currency,
            Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text,
            Helpers.XlsxColumn.Text, Helpers.XlsxColumn.Text);
        foreach (var item in items)
        {
            xlsx.AddRow(
                item.Type.ToString(),
                item.Label,
                item.Amount,
                item.Frequency.ToString(),
                Helpers.Format.Date(item.NextDueDate),
                item.EndDate is null ? "—" : Helpers.Format.Date(item.EndDate.Value),
                item.IsActive ? "Active" : "Paused",
                item.Note);
        }
        return xlsx.ToFileResult($"finapp-recurring-{DateTime.Today:yyyy-MM-dd}.xlsx");
    }

    // ------------------------------------------------------------ helpers

    private RecurringTransactionFormViewModel BuildForm(RecurringTransactionFormViewModel model)
    {
        model.Budgets = model.Type == RecurringTransactionType.Expense
            ? _budgets.GetOptionsForExpense(CurrentUser.Id, model.Label, model.StartDate)
            : new List<BudgetOption>();
        return model;
    }

    /// <summary>When Label is "Other", requires the custom text the user typed
    /// and stores it as the label. Custom text posted while a catalog value is
    /// picked (field is hidden) is ignored.</summary>
    private void ResolveLabel(RecurringTransactionFormViewModel model)
    {
        if (model.Label != "Other") return;

        var custom = model.CustomLabel?.Trim() ?? string.Empty;
        if (custom.Length == 0)
        {
            ModelState.AddModelError(nameof(model.CustomLabel),
                model.Type == RecurringTransactionType.Expense ? "Please enter your category." : "Please enter your income source.");
            return;
        }

        model.Label = custom;
    }

    private static void RestoreLabelForDisplay(RecurringTransactionFormViewModel model)
    {
        var catalog = model.Type == RecurringTransactionType.Expense ? Catalog.ExpenseCategories : Catalog.IncomeSources;
        if (model.CustomLabel is { } custom &&
            custom.Length > 0 &&
            !catalog.Contains(model.Label, StringComparer.OrdinalIgnoreCase) &&
            model.Label.Equals(custom, StringComparison.OrdinalIgnoreCase))
        {
            model.Label = "Other";
        }
    }

    /// <summary>Validates the budget picked for a recurring expense (must exist
    /// on this account and match the category). Unlike a one-off expense, period
    /// coverage isn't checked here — a recurring bill naturally outlives any
    /// single budget period.</summary>
    private Budget? ResolveBudget(RecurringTransactionFormViewModel model)
    {
        if (model.Type != RecurringTransactionType.Expense || model.BudgetId is null) return null;

        var budget = _budgets.Get(CurrentUser.Id, model.BudgetId.Value);
        if (budget is null)
        {
            ModelState.AddModelError(nameof(model.BudgetId), "The selected budget no longer exists. Please pick another one.");
            return null;
        }

        if (!budget.Category.Equals(model.Label, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.BudgetId), $"The selected budget is for {budget.Category}, not {model.Label}.");
            return null;
        }

        return budget;
    }
}
