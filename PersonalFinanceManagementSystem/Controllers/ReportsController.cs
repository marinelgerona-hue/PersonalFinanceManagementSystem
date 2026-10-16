using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Reports — monthly summary, expense analysis, savings, budgets, income, annual + Excel exports.</summary>
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

        var now = DateTime.Now;
        var cur = CurrentUser.Currency;

        // One cheap call into each sub-report so its card can show a real, live headline
        // number instead of just a static description — same data each report page itself uses.
        var monthly = _reports.GetMonthlySummary(CurrentUser.Id, now.Year, now.Month);
        var expenseAnalysis = _reports.GetExpenseAnalysis(CurrentUser.Id, now.Year, now.Month);
        var savings = _reports.GetSavingsReport(CurrentUser.Id);
        var budgets = _reports.GetBudgetReport(CurrentUser.Id, now.Year, now.Month);
        var income = _reports.GetIncomeReport(CurrentUser.Id);
        var annual = _reports.GetAnnualSummary(CurrentUser.Id, now.Year);
        var topCategory = expenseAnalysis.Categories.FirstOrDefault();

        var vm = new ReportsLandingViewModel
        {
            Currency = cur,
            MonthLabel = monthly.MonthLabel,
            MonthIncome = monthly.TotalIncome,
            MonthExpenses = monthly.TotalExpenses,
            MonthSavingsRate = monthly.TotalIncome > 0 ? Math.Round(monthly.Net / monthly.TotalIncome * 100, 1) : 0,
            Cards = new()
            {
                new("monthly", "i-calendar", "#6366f1", "Monthly Summary",
                    $"Income, expenses, net savings and budgets for {monthly.MonthLabel}.", "Monthly",
                    "Net saved this month", Format.Money(monthly.Net, cur)),

                new("expenses", "i-expense", "#ef4444", "Expense Analysis",
                    "Where your money goes — category breakdown, averages and biggest expenses.", "ExpenseAnalysis",
                    topCategory is null ? "This month" : "Top category",
                    topCategory is null ? "No expenses yet" : $"{topCategory.Category} · {Format.Pct(topCategory.Percent)}%"),

                new("savings", "i-piggy", "#0ea5e9", "Savings Report",
                    "Goal progress, totals and how much you set aside each month.", "Savings",
                    "Overall funded", savings.TotalTarget > 0 ? $"{Format.Pct(savings.OverallPercent)}%" : "No goals yet"),

                new("budgets", "i-budget", "#f59e0b", "Budget Report",
                    "Budget vs actual spending per category, with over-budget warnings.", "Budgets",
                    "This month",
                    budgets.Budgets.Count == 0 ? "No budgets set" : budgets.OverCount > 0 ? $"{budgets.OverCount} over budget" : "All on track",
                    StatNeedsAttention: budgets.OverCount > 0),

                new("income", "i-income", "#10b981", "Income Report",
                    "Income by source, monthly trend and your best month.", "Income",
                    "Received this month", Format.Money(income.ThisMonth, cur)),

                new("annual", "i-report", "#7c3aed", "Annual Summary",
                    $"Month-by-month view of {now.Year} with totals and top categories.", "Annual",
                    $"Net for {now.Year}", Format.Money(annual.TotalNet, cur)),

                new("range", "i-calendar", "#14b8a6", "Date Range",
                    "Pick any two dates for an exact income, expense and category total.", "DateRange",
                    "Flexible range", "Any custom dates")
            }
        };
        return View(vm);
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

    // ============================================================ Excel exports

    /// <summary>Monthly summary — every income and expense transaction of the month.</summary>
    [HttpGet]
    public IActionResult ExportMonthly(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var csv = new XlsxFile(Format.Symbol(CurrentUser.Currency), new[] { "Date", "Type", "Category / Source", "Amount", "Note" },
            XlsxColumn.Text, XlsxColumn.Text, XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Text);
        foreach (var income in _incomes.ForMonth(CurrentUser.Id, y, m))
        {
            csv.AddRow(income.Date, "Income", income.Source, income.Amount, income.Note);
        }
        foreach (var expense in _expenses.ForMonth(CurrentUser.Id, y, m))
        {
            csv.AddRow(expense.Date, "Expense", expense.Category, expense.Amount, expense.Note);
        }
        return csv.ToFileResult($"monthly-summary-{y:0000}-{m:00}.xlsx");
    }

    /// <summary>Expense analysis — category breakdown with share, count and average.</summary>
    [HttpGet]
    public IActionResult ExportExpenseAnalysis(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var vm = _reports.GetExpenseAnalysis(CurrentUser.Id, y, m);

        var csv = new XlsxFile(Format.Symbol(CurrentUser.Currency), new[] { "Category", "Amount", "Share %", "Records", "Average" },
            XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Percent, XlsxColumn.Integer, XlsxColumn.Currency);
        foreach (var c in vm.Categories)
        {
            csv.AddRow(c.Category, c.Amount, c.Percent, c.Count, c.Average);
        }
        csv.AddTotalRow("TOTAL", vm.Total, vm.Categories.Count > 0 ? 100m : 0m, vm.Categories.Sum(c => c.Count), string.Empty);
        return csv.ToFileResult($"expense-analysis-{y:0000}-{m:00}.xlsx");
    }

    /// <summary>Savings report — goal progress and totals.</summary>
    [HttpGet]
    public IActionResult ExportSavings()
    {
        var vm = _reports.GetSavingsReport(CurrentUser.Id);

        var csv = new XlsxFile(Format.Symbol(CurrentUser.Currency), new[] { "Goal", "Saved", "Target", "Remaining", "Progress %", "Target date", "Status" },
            XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Currency, XlsxColumn.Currency, XlsxColumn.Percent, XlsxColumn.Text, XlsxColumn.Text);
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
        csv.AddTotalRow("TOTAL", vm.TotalSaved, vm.TotalTarget, vm.TotalRemaining, vm.OverallPercent, string.Empty, string.Empty);
        return csv.ToFileResult($"savings-report-{DateTime.Today:yyyy-MM-dd}.xlsx");
    }

    /// <summary>Budget report — budget vs actual spending for one month.</summary>
    [HttpGet]
    public IActionResult ExportBudgets(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var vm = _reports.GetBudgetReport(CurrentUser.Id, y, m);

        var csv = new XlsxFile(Format.Symbol(CurrentUser.Currency), new[] { "Category", "Budget", "Spent", "Remaining", "Used %", "Status" },
            XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Currency, XlsxColumn.Currency, XlsxColumn.Percent, XlsxColumn.Text);
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
        csv.AddTotalRow("TOTAL", vm.TotalBudget, vm.TotalSpent, vm.TotalRemaining, vm.OverallPercent, string.Empty);
        return csv.ToFileResult($"budget-report-{y:0000}-{m:00}.xlsx");
    }

    /// <summary>Income report — all-time totals by source plus the 12-month trend.</summary>
    [HttpGet]
    public IActionResult ExportIncome()
    {
        var vm = _reports.GetIncomeReport(CurrentUser.Id);

        var csv = new XlsxFile(Format.Symbol(CurrentUser.Currency), new[] { "Source", "Amount", "Share %", "Records", "Average" },
            XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Percent, XlsxColumn.Integer, XlsxColumn.Currency);
        foreach (var s in vm.Sources)
        {
            csv.AddRow(s.Source, s.Amount, s.Percent, s.Count, s.Average);
        }
        csv.AddTotalRow("TOTAL", vm.Total, vm.Sources.Count > 0 ? 100m : 0m, vm.RecordCount, string.Empty);

        csv.AddSection(new[] { "Month", "Income", "Expenses" }, XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Currency);
        foreach (var t in vm.Trend)
        {
            csv.AddRow($"{t.Year:0000}-{t.Month:00}", t.Income, t.Expenses);
        }
        return csv.ToFileResult($"income-report-{DateTime.Today:yyyy-MM-dd}.xlsx");
    }

    /// <summary>Date-range report — every transaction inside the chosen range.</summary>
    [HttpGet]
    public IActionResult ExportDateRange(DateTime? from, DateTime? to)
    {
        var f = (from ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)).Date;
        var t = (to ?? DateTime.Today).Date;
        var vm = _reports.GetDateRange(CurrentUser.Id, f, t);

        var csv = new XlsxFile(Format.Symbol(CurrentUser.Currency), new[] { "Date", "Type", "Category / Source", "Amount", "Note" },
            XlsxColumn.Text, XlsxColumn.Text, XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Text);
        foreach (var income in vm.IncomeItems)
        {
            csv.AddRow(income.Date, "Income", income.Source, income.Amount, income.Note);
        }
        foreach (var expense in vm.ExpenseItems)
        {
            csv.AddRow(expense.Date, "Expense", expense.Category, expense.Amount, expense.Note);
        }
        csv.AddTotalRow($"RANGE TOTAL {vm.From:yyyy-MM-dd} to {vm.To:yyyy-MM-dd}", "Income", string.Empty, vm.TotalIncome, string.Empty);
        csv.AddTotalRow(string.Empty, "Expenses", string.Empty, vm.TotalExpenses, string.Empty);
        csv.AddTotalRow(string.Empty, "Remaining", string.Empty, vm.RemainingBalance, string.Empty);
        return csv.ToFileResult($"date-range-{vm.From:yyyy-MM-dd}-to-{vm.To:yyyy-MM-dd}.xlsx");
    }

    /// <summary>Annual summary — month-by-month income, expenses, net and savings.</summary>
    [HttpGet]
    public IActionResult ExportAnnual(int? year)
    {
        var vm = _reports.GetAnnualSummary(CurrentUser.Id, year ?? DateTime.Now.Year);

        var csv = new XlsxFile(Format.Symbol(CurrentUser.Currency), new[] { "Month", "Income", "Expenses", "Net", "To goals" },
            XlsxColumn.Text, XlsxColumn.Currency, XlsxColumn.Currency, XlsxColumn.Currency, XlsxColumn.Currency);
        foreach (var m in vm.Months)
        {
            csv.AddRow(Format.MonthName(m.Month), m.Income, m.Expenses, m.Net, m.Saved);
        }
        csv.AddTotalRow("TOTAL", vm.TotalIncome, vm.TotalExpenses, vm.TotalNet, vm.TotalSaved);
        return csv.ToFileResult($"annual-summary-{vm.Year}.xlsx");
    }

    // ============================================================ PDF exports
    // Same report data as the Excel exports above, just a second output format —
    // each reuses the identical _reports.GetXxx(...) call, no new queries.

    private string Money(decimal amount) => $"{amount:N2} {CurrentUser.Currency}";
    private static string Pct(decimal value) => $"{Format.Pct(value)}%";

    [HttpGet]
    public IActionResult ExportMonthlyPdf(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var vm = _reports.GetMonthlySummary(CurrentUser.Id, y, m);

        var pdf = new PdfFile("Monthly Summary", vm.MonthLabel)
            .AddSummaryLine("Total income", Money(vm.TotalIncome))
            .AddSummaryLine("Total expenses", Money(vm.TotalExpenses))
            .AddSummaryLine("Net", Money(vm.Net))
            .AddTable(
                new[] { "Date", "Type", "Category / Source", "Amount", "Note" },
                new float[] { 12, 12, 22, 14, 40 },
                new[] { false, false, false, true, false });

        foreach (var income in _incomes.ForMonth(CurrentUser.Id, y, m))
        {
            pdf.AddRow(Format.Date(income.Date), "Income", income.Source, Money(income.Amount), income.Note);
        }
        foreach (var expense in _expenses.ForMonth(CurrentUser.Id, y, m))
        {
            pdf.AddRow(Format.Date(expense.Date), "Expense", expense.Category, Money(expense.Amount), expense.Note);
        }
        return pdf.ToFileResult($"monthly-summary-{y:0000}-{m:00}.pdf");
    }

    [HttpGet]
    public IActionResult ExportExpenseAnalysisPdf(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var vm = _reports.GetExpenseAnalysis(CurrentUser.Id, y, m);

        var pdf = new PdfFile("Expense Analysis", vm.MonthLabel)
            .AddSummaryLine("Total spent", Money(vm.Total))
            .AddSummaryLine("Average per day", Money(vm.AveragePerDay))
            .AddTable(
                new[] { "Category", "Amount", "Share %", "Records", "Average" },
                new float[] { 30, 18, 15, 15, 22 },
                new[] { false, true, true, true, true });

        foreach (var c in vm.Categories)
        {
            pdf.AddRow(c.Category, Money(c.Amount), Pct(c.Percent), c.Count.ToString(), Money(c.Average));
        }
        pdf.AddTotalRow("TOTAL", Money(vm.Total), vm.Categories.Count > 0 ? "100.0%" : "0.0%", vm.Categories.Sum(c => c.Count).ToString(), "");
        return pdf.ToFileResult($"expense-analysis-{y:0000}-{m:00}.pdf");
    }

    [HttpGet]
    public IActionResult ExportSavingsPdf()
    {
        var vm = _reports.GetSavingsReport(CurrentUser.Id);

        var pdf = new PdfFile("Savings Report")
            .AddSummaryLine("Total saved", Money(vm.TotalSaved))
            .AddSummaryLine("Total target", Money(vm.TotalTarget))
            .AddSummaryLine("Overall funded", Pct(vm.OverallPercent))
            .AddTable(
                new[] { "Goal", "Saved", "Target", "Remaining", "Progress %", "Target date", "Status" },
                new float[] { 20, 13, 13, 13, 12, 14, 15 },
                new[] { false, true, true, true, true, false, false });

        foreach (var g in vm.Goals)
        {
            pdf.AddRow(
                g.Goal.Name, Money(g.Saved), Money(g.Target), Money(g.Remaining), Pct(g.Percent),
                g.Goal.Deadline is DateTime dl ? Format.Date(dl) : "-",
                g.Completed ? "Completed" : "In progress");
        }
        pdf.AddTotalRow("TOTAL", Money(vm.TotalSaved), Money(vm.TotalTarget), Money(vm.TotalRemaining), Pct(vm.OverallPercent), "", "");
        return pdf.ToFileResult($"savings-report-{DateTime.Today:yyyy-MM-dd}.pdf");
    }

    [HttpGet]
    public IActionResult ExportBudgetsPdf(int? year, int? month)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var vm = _reports.GetBudgetReport(CurrentUser.Id, y, m);

        var pdf = new PdfFile("Budget Report", vm.MonthLabel)
            .AddSummaryLine("Total budgeted", Money(vm.TotalBudget))
            .AddSummaryLine("Total spent", Money(vm.TotalSpent))
            .AddTable(
                new[] { "Category", "Budget", "Spent", "Remaining", "Used %", "Status" },
                new float[] { 20, 16, 16, 16, 14, 18 },
                new[] { false, true, true, true, true, false });

        foreach (var b in vm.Budgets)
        {
            pdf.AddRow(
                b.Budget.Category, Money(b.Budget.Limit), Money(b.Spent), Money(b.Remaining), Pct(b.PercentUsed),
                b.IsOver ? "Over budget" : b.IsNear ? "Near limit" : "On track");
        }
        pdf.AddTotalRow("TOTAL", Money(vm.TotalBudget), Money(vm.TotalSpent), Money(vm.TotalRemaining), Pct(vm.OverallPercent), "");
        return pdf.ToFileResult($"budget-report-{y:0000}-{m:00}.pdf");
    }

    /// <summary>Income by source — the 12-month trend grid is web-only; the PDF statement
    /// covers the headline totals and source breakdown.</summary>
    [HttpGet]
    public IActionResult ExportIncomePdf()
    {
        var vm = _reports.GetIncomeReport(CurrentUser.Id);

        var pdf = new PdfFile("Income Report")
            .AddSummaryLine("Total income", Money(vm.Total))
            .AddSummaryLine("This month", Money(vm.ThisMonth))
            .AddSummaryLine("Average monthly", Money(vm.AverageMonthly))
            .AddTable(
                new[] { "Source", "Amount", "Share %", "Records", "Average" },
                new float[] { 30, 18, 15, 15, 22 },
                new[] { false, true, true, true, true });

        foreach (var s in vm.Sources)
        {
            pdf.AddRow(s.Source, Money(s.Amount), Pct(s.Percent), s.Count.ToString(), Money(s.Average));
        }
        pdf.AddTotalRow("TOTAL", Money(vm.Total), vm.Sources.Count > 0 ? "100.0%" : "0.0%", vm.RecordCount.ToString(), "");
        return pdf.ToFileResult($"income-report-{DateTime.Today:yyyy-MM-dd}.pdf");
    }

    [HttpGet]
    public IActionResult ExportDateRangePdf(DateTime? from, DateTime? to)
    {
        var f = (from ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)).Date;
        var t = (to ?? DateTime.Today).Date;
        var vm = _reports.GetDateRange(CurrentUser.Id, f, t);

        var pdf = new PdfFile("Date Range Report", $"{Format.Date(vm.From)} to {Format.Date(vm.To)}")
            .AddSummaryLine("Total income", Money(vm.TotalIncome))
            .AddSummaryLine("Total expenses", Money(vm.TotalExpenses))
            .AddSummaryLine("Remaining", Money(vm.RemainingBalance))
            .AddTable(
                new[] { "Date", "Type", "Category / Source", "Amount", "Note" },
                new float[] { 12, 12, 22, 14, 40 },
                new[] { false, false, false, true, false });

        foreach (var income in vm.IncomeItems)
        {
            pdf.AddRow(Format.Date(income.Date), "Income", income.Source, Money(income.Amount), income.Note);
        }
        foreach (var expense in vm.ExpenseItems)
        {
            pdf.AddRow(Format.Date(expense.Date), "Expense", expense.Category, Money(expense.Amount), expense.Note);
        }
        return pdf.ToFileResult($"date-range-{vm.From:yyyy-MM-dd}-to-{vm.To:yyyy-MM-dd}.pdf");
    }

    [HttpGet]
    public IActionResult ExportAnnualPdf(int? year)
    {
        var vm = _reports.GetAnnualSummary(CurrentUser.Id, year ?? DateTime.Now.Year);

        var pdf = new PdfFile("Annual Summary", vm.Year.ToString())
            .AddSummaryLine("Total income", Money(vm.TotalIncome))
            .AddSummaryLine("Total expenses", Money(vm.TotalExpenses))
            .AddSummaryLine("Net", Money(vm.TotalNet))
            .AddTable(
                new[] { "Month", "Income", "Expenses", "Net", "To goals" },
                new float[] { 24, 19, 19, 19, 19 },
                new[] { false, true, true, true, true });

        foreach (var mo in vm.Months)
        {
            pdf.AddRow(Format.MonthName(mo.Month), Money(mo.Income), Money(mo.Expenses), Money(mo.Net), Money(mo.Saved));
        }
        pdf.AddTotalRow("TOTAL", Money(vm.TotalIncome), Money(vm.TotalExpenses), Money(vm.TotalNet), Money(vm.TotalSaved));
        return pdf.ToFileResult($"annual-summary-{vm.Year}.pdf");
    }
}
