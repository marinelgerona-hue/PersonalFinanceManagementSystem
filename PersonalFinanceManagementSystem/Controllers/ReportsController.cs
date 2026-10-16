using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Reports — monthly summary, expense analysis, savings, budgets, income, annual + CSV exports.</summary>
public class ReportsController : AppControllerBase
{
    private readonly IReportService _reports;
    private readonly IIncomeService _incomes;
    private readonly IExpenseService _expenses;

    public ReportsController(
        IAuthService auth,
        IReportService reports,
        IIncomeService incomes,
        IExpenseService expenses) : base(auth)
    {
        _reports = reports;
        _incomes = incomes;
        _expenses = expenses;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Reports";
        ViewData["ActiveNav"] = "reports";
        return View();
    }

    public IActionResult Monthly(int? year, int? month)
    {
        var now = DateTime.Now;
        var vm = _reports.GetMonthlySummary(CurrentUser.Id, year ?? now.Year, month ?? now.Month);
        ViewData["Title"] = "Monthly Summary";
        ViewData["ActiveNav"] = "reports";
        ViewData["ActiveReport"] = "monthly";
        return View(vm);
    }

    public IActionResult ExpenseAnalysis(int? year, int? month)
    {
        var now = DateTime.Now;
        var vm = _reports.GetExpenseAnalysis(CurrentUser.Id, year ?? now.Year, month ?? now.Month);
        ViewData["Title"] = "Expense Analysis";
        ViewData["ActiveNav"] = "reports";
        ViewData["ActiveReport"] = "expenses";
        return View(vm);
    }

    public IActionResult Savings()
    {
        ViewData["Title"] = "Savings Report";
        ViewData["ActiveNav"] = "reports";
        ViewData["ActiveReport"] = "savings";
        return View(_reports.GetSavingsReport(CurrentUser.Id));
    }

    public IActionResult Budgets(int? year, int? month)
    {
        var now = DateTime.Now;
        var vm = _reports.GetBudgetReport(CurrentUser.Id, year ?? now.Year, month ?? now.Month);
        ViewData["Title"] = "Budget Report";
        ViewData["ActiveNav"] = "reports";
        ViewData["ActiveReport"] = "budgets";
        return View(vm);
    }

    public IActionResult Income()
    {
        ViewData["Title"] = "Income Report";
        ViewData["ActiveNav"] = "reports";
        ViewData["ActiveReport"] = "income";
        return View(_reports.GetIncomeReport(CurrentUser.Id));
    }

    public IActionResult Annual(int? year)
    {
        var vm = _reports.GetAnnualSummary(CurrentUser.Id, year ?? DateTime.Now.Year);
        ViewData["Title"] = "Annual Summary";
        ViewData["ActiveNav"] = "reports";
        ViewData["ActiveReport"] = "annual";
        return View(vm);
    }

    /// <summary>Custom report with exact totals for a user-chosen date range.</summary>
    public IActionResult DateRange(DateTime? from, DateTime? to)
    {
        var f = (from ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)).Date;
        var t = (to ?? DateTime.Today).Date;

        var vm = _reports.GetDateRange(CurrentUser.Id, f, t);
        ViewData["Title"] = "Date Range Report";
        ViewData["ActiveNav"] = "reports";
        ViewData["ActiveReport"] = "range";
        return View(vm);
    }

    // ============================================================ CSV exports

    /// <summary>Monthly summary — every income and expense transaction of the month.</summary>
    [HttpGet]
    public IActionResult ExportMonthly(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var csv = new CsvFile("Date", "Type", "Category / Source", "Amount", "Note");
        foreach (var income in _incomes.ForMonth(CurrentUser.Id, y, m))
        {
            csv.AddRow(income.Date, "Income", income.Source, income.Amount, income.Note);
        }
        foreach (var expense in _expenses.ForMonth(CurrentUser.Id, y, m))
        {
            csv.AddRow(expense.Date, "Expense", expense.Category, expense.Amount, expense.Note);
        }
        return csv.ToFileResult($"monthly-summary-{y:0000}-{m:00}.csv");
    }

    /// <summary>Expense analysis — category breakdown with share, count and average.</summary>
    [HttpGet]
    public IActionResult ExportExpenseAnalysis(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var vm = _reports.GetExpenseAnalysis(CurrentUser.Id, y, m);

        var csv = new CsvFile("Category", "Amount", "Share %", "Records", "Average");
        foreach (var c in vm.Categories)
        {
            csv.AddRow(c.Category, c.Amount, c.Percent, c.Count, c.Average);
        }
        csv.AddRow("TOTAL", vm.Total, vm.Categories.Count > 0 ? 100m : 0m, vm.Categories.Sum(c => c.Count), string.Empty);
        return csv.ToFileResult($"expense-analysis-{y:0000}-{m:00}.csv");
    }

    /// <summary>Savings report — goal progress and totals.</summary>
    [HttpGet]
    public IActionResult ExportSavings()
    {
        var vm = _reports.GetSavingsReport(CurrentUser.Id);

        var csv = new CsvFile("Goal", "Saved", "Target", "Remaining", "Progress %", "Target date", "Status");
        foreach (var g in vm.Goals)
        {
            csv.AddRow(
                g.Goal.Name,
                g.Saved,
                g.Target,
                g.Remaining,
                g.Percent,
                g.Goal.Deadline,
                g.Completed ? "Completed" : "In progress");
        }
        csv.AddRow("TOTAL", vm.TotalSaved, vm.TotalTarget, vm.TotalRemaining, vm.OverallPercent, string.Empty, string.Empty);
        return csv.ToFileResult($"savings-report-{DateTime.Today:yyyy-MM-dd}.csv");
    }

    /// <summary>Budget report — budget vs actual spending for one month.</summary>
    [HttpGet]
    public IActionResult ExportBudgets(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var vm = _reports.GetBudgetReport(CurrentUser.Id, y, m);

        var csv = new CsvFile("Category", "Budget", "Spent", "Remaining", "Used %", "Status");
        foreach (var b in vm.Budgets)
        {
            csv.AddRow(
                b.Budget.Category,
                b.Budget.Limit,
                b.Spent,
                b.Remaining,
                b.PercentUsed,
                b.IsOver ? "Over budget" : b.IsNear ? "Near limit" : "On track");
        }
        csv.AddRow("TOTAL", vm.TotalBudget, vm.TotalSpent, vm.TotalRemaining, vm.OverallPercent, string.Empty);
        return csv.ToFileResult($"budget-report-{y:0000}-{m:00}.csv");
    }

    /// <summary>Income report — all-time totals by source plus the 12-month trend.</summary>
    [HttpGet]
    public IActionResult ExportIncome()
    {
        var vm = _reports.GetIncomeReport(CurrentUser.Id);

        var csv = new CsvFile("Source", "Amount", "Share %", "Records", "Average");
        foreach (var s in vm.Sources)
        {
            csv.AddRow(s.Source, s.Amount, s.Percent, s.Count, s.Average);
        }
        csv.AddRow("TOTAL", vm.Total, vm.Sources.Count > 0 ? 100m : 0m, vm.RecordCount, string.Empty);

        csv.AddRow(string.Empty);
        csv.AddRow("Month", "Income", "Expenses");
        foreach (var t in vm.Trend)
        {
            csv.AddRow($"{t.Year:0000}-{t.Month:00}", t.Income, t.Expenses);
        }
        return csv.ToFileResult($"income-report-{DateTime.Today:yyyy-MM-dd}.csv");
    }

    /// <summary>Date-range report — every transaction inside the chosen range.</summary>
    [HttpGet]
    public IActionResult ExportDateRange(DateTime? from, DateTime? to)
    {
        var f = (from ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)).Date;
        var t = (to ?? DateTime.Today).Date;
        var vm = _reports.GetDateRange(CurrentUser.Id, f, t);

        var csv = new CsvFile("Date", "Type", "Category / Source", "Amount", "Note");
        foreach (var income in vm.IncomeItems)
        {
            csv.AddRow(income.Date, "Income", income.Source, income.Amount, income.Note);
        }
        foreach (var expense in vm.ExpenseItems)
        {
            csv.AddRow(expense.Date, "Expense", expense.Category, expense.Amount, expense.Note);
        }
        csv.AddRow(string.Empty);
        csv.AddRow($"RANGE TOTAL {vm.From:yyyy-MM-dd} to {vm.To:yyyy-MM-dd}", "Income", string.Empty, vm.TotalIncome, string.Empty);
        csv.AddRow(string.Empty, "Expenses", string.Empty, vm.TotalExpenses, string.Empty);
        csv.AddRow(string.Empty, "Remaining", string.Empty, vm.RemainingBalance, string.Empty);
        return csv.ToFileResult($"date-range-{vm.From:yyyy-MM-dd}-to-{vm.To:yyyy-MM-dd}.csv");
    }

    /// <summary>Annual summary — month-by-month income, expenses, net and savings.</summary>
    [HttpGet]
    public IActionResult ExportAnnual(int? year)
    {
        var vm = _reports.GetAnnualSummary(CurrentUser.Id, year ?? DateTime.Now.Year);

        var csv = new CsvFile("Month", "Income", "Expenses", "Net", "To goals");
        foreach (var m in vm.Months)
        {
            csv.AddRow(Format.MonthName(m.Month), m.Income, m.Expenses, m.Net, m.Saved);
        }
        csv.AddRow("TOTAL", vm.TotalIncome, vm.TotalExpenses, vm.TotalNet, vm.TotalSaved);
        return csv.ToFileResult($"annual-summary-{vm.Year}.csv");
    }
}
