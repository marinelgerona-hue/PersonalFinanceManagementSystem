using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// Builds dashboard and report view models from database-backed services.
/// </summary>
public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IIncomeService _incomes;
    private readonly IExpenseService _expenses;
    private readonly IBudgetService _budgets;
    private readonly ISavingsGoalService _goals;

    public ReportService(
        ApplicationDbContext db,
        IIncomeService incomes,
        IExpenseService expenses,
        IBudgetService budgets,
        ISavingsGoalService goals)
    {
        _db = db;
        _incomes = incomes;
        _expenses = expenses;
        _budgets = budgets;
        _goals = goals;
    }

    private string Currency(int userId)
    {
        return _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Currency)
            .FirstOrDefault() ?? "USD";
    }

    // ------------------------------------------------------------------ dashboard

    public DashboardViewModel GetDashboard(int userId)
    {
        var now = DateTime.Now;

        var vm = new DashboardViewModel
        {
            Year = now.Year,
            Month = now.Month,
            MonthLabel = now.ToString("MMMM yyyy"),

            TotalIncome = _incomes.Total(userId),
            TotalExpenses = _expenses.Total(userId),

            MonthlyIncome = _incomes.TotalForMonth(
                userId,
                now.Year,
                now.Month),

            MonthlyExpenses = _expenses.TotalForMonth(
                userId,
                now.Year,
                now.Month),

            Currency = Currency(userId),

            Spending = _expenses.SpendingByCategory(
                userId,
                now.Year,
                now.Month),

            Trend = MonthlyIncomeExpenseTrend(userId, 6),

            Goals = _goals.GetProgress(userId),

            Budgets = _budgets.GetStatusForMonth(
                userId,
                now.Year,
                now.Month),

            Recent = RecentTransactions(userId, 6)
        };

        vm.Balance = vm.TotalIncome - vm.TotalExpenses;

        vm.TotalSaved = vm.Goals.Sum(g => g.Saved);

        vm.SavingsTarget = vm.Goals.Sum(g => g.Target);

        vm.SavingsPercent =
            vm.SavingsTarget > 0
                ? Math.Round(
                    vm.TotalSaved /
                    vm.SavingsTarget *
                    100,
                    1)
                : 0;

        return vm;
    }

    // ------------------------------------------------------------------ monthly trend

    /// <summary>
    /// Income vs expenses for the last N months.
    /// Oldest month appears first.
    /// </summary>
    private List<MonthPoint> MonthlyIncomeExpenseTrend(
        int userId,
        int months)
    {
        var result = new List<MonthPoint>();

        var current = new DateTime(
            DateTime.Now.Year,
            DateTime.Now.Month,
            1);

        for (var i = months - 1; i >= 0; i--)
        {
            var month = current.AddMonths(-i);

            result.Add(new MonthPoint
            {
                Year = month.Year,
                Month = month.Month,

                Label = Format.MonthShort(
                    month.Month),

                Income = _incomes.TotalForMonth(
                    userId,
                    month.Year,
                    month.Month),

                Expenses = _expenses.TotalForMonth(
                    userId,
                    month.Year,
                    month.Month)
            });
        }

        return result;
    }

    // ------------------------------------------------------------------ recent transactions

    private List<RecentTransaction> RecentTransactions(
        int userId,
        int count)
    {
        var incomeRows = _incomes
            .GetAll(userId)
            .Select(i => new RecentTransaction
            {
                Date = i.Date,
                Title = i.Source,
                Category = i.Source,
                Amount = i.Amount,
                IsIncome = true
            });

        var expenseRows = _expenses
            .GetAll(userId)
            .Select(e => new RecentTransaction
            {
                Date = e.Date,

                Title = string.IsNullOrWhiteSpace(e.Note)
                    ? e.Category
                    : e.Note,

                Category = e.Category,
                Amount = e.Amount,
                IsIncome = false
            });

        return incomeRows
            .Concat(expenseRows)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.IsIncome)
            .Take(count)
            .ToList();
    }

    // ------------------------------------------------------------------ monthly summary

    public MonthlySummaryViewModel GetMonthlySummary(
        int userId,
        int year,
        int month)
    {
        var incomeItems =
            _incomes.ForMonth(
                userId,
                year,
                month);

        var expenseItems =
            _expenses.ForMonth(
                userId,
                year,
                month);

        var now = DateTime.Now;

        var daysInMonth =
            DateTime.DaysInMonth(
                year,
                month);

        var daysElapsed =
            (year == now.Year && month == now.Month)
                ? now.Day
                : daysInMonth;

        return new MonthlySummaryViewModel
        {
            Year = year,
            Month = month,

            MonthLabel =
                $"{Format.MonthName(month)} {year}",

            Currency = Currency(userId),

            TotalIncome =
                incomeItems.Sum(i => i.Amount),

            TotalExpenses =
                expenseItems.Sum(e => e.Amount),

            Saved =
                _goals.SavedInMonth(
                    userId,
                    year,
                    month),

            IncomeCount =
                incomeItems.Count,

            ExpenseCount =
                expenseItems.Count,

            DaysElapsed = daysElapsed,

            LargestExpense =
                expenseItems
                    .OrderByDescending(e => e.Amount)
                    .FirstOrDefault(),

            LargestIncome =
                incomeItems
                    .OrderByDescending(i => i.Amount)
                    .FirstOrDefault(),

            TopCategories =
                _expenses
                    .SpendingByCategory(
                        userId,
                        year,
                        month)
                    .Take(5)
                    .ToList(),

            Budgets =
                _budgets.GetStatusForMonth(
                    userId,
                    year,
                    month)
        };
    }

    // ------------------------------------------------------------------ expense analysis

    public ExpenseAnalysisViewModel GetExpenseAnalysis(
        int userId,
        int year,
        int month)
    {
        var now = DateTime.Now;

        var daysInMonth =
            DateTime.DaysInMonth(
                year,
                month);

        var daysElapsed =
            (year == now.Year && month == now.Month)
                ? now.Day
                : daysInMonth;

        var total =
            _expenses.TotalForMonth(
                userId,
                year,
                month);

        var previous =
            new DateTime(year, month, 1)
                .AddMonths(-1);

        return new ExpenseAnalysisViewModel
        {
            Year = year,
            Month = month,

            MonthLabel =
                $"{Format.MonthName(month)} {year}",

            Currency = Currency(userId),

            Total = total,

            PreviousMonthTotal =
                _expenses.TotalForMonth(
                    userId,
                    previous.Year,
                    previous.Month),

            AveragePerDay =
                daysElapsed > 0
                    ? Math.Round(
                        total / daysElapsed,
                        2)
                    : 0,

            LargestExpense =
                _expenses.LargestInMonth(
                    userId,
                    year,
                    month),

            Categories =
                _expenses.SpendingByCategory(
                    userId,
                    year,
                    month)
        };
    }

    // ------------------------------------------------------------------ savings report

    public SavingsReportViewModel GetSavingsReport(
        int userId)
    {
        var now = DateTime.Now;

        var goals =
            _goals.GetProgress(userId);

        return new SavingsReportViewModel
        {
            Currency = Currency(userId),

            TotalSaved =
                goals.Sum(g => g.Saved),

            TotalTarget =
                goals.Sum(g => g.Target),

            ThisMonthSaved =
                _goals.SavedInMonth(
                    userId,
                    now.Year,
                    now.Month),

            GoalsCompleted =
                goals.Count(g => g.Completed),

            Goals = goals,

            MonthlyContributions =
                _goals.MonthlyContributions(
                    userId,
                    6)
        };
    }

    // ------------------------------------------------------------------ budget report

    public BudgetReportViewModel GetBudgetReport(
        int userId,
        int year,
        int month)
    {
        return new BudgetReportViewModel
        {
            Year = year,
            Month = month,

            MonthLabel =
                $"{Format.MonthName(month)} {year}",

            Currency = Currency(userId),

            Budgets =
                _budgets.GetStatusForMonth(
                    userId,
                    year,
                    month)
        };
    }

    // ------------------------------------------------------------------ income report

    public IncomeReportViewModel GetIncomeReport(
        int userId)
    {
        var now = DateTime.Now;

        var all =
            _incomes.GetAll(userId);

        var trend =
            MonthlyIncomeExpenseTrend(
                userId,
                12);

        var total =
            all.Sum(i => i.Amount);

        var bySource = all
            .GroupBy(i => i.Source)
            .Select(g => new SourceSlice
            {
                Source = g.Key,

                Amount =
                    g.Sum(i => i.Amount),

                Count =
                    g.Count(),

                Percent =
                    total > 0
                        ? Math.Round(
                            g.Sum(i => i.Amount) /
                            total *
                            100,
                            1)
                        : 0
            })
            .OrderByDescending(s => s.Amount)
            .ToList();

        var monthsWithData =
            trend.Count(t => t.Income > 0);

        return new IncomeReportViewModel
        {
            Currency = Currency(userId),

            Total = total,

            ThisMonth =
                _incomes.TotalForMonth(
                    userId,
                    now.Year,
                    now.Month),

            AverageMonthly =
                monthsWithData > 0
                    ? Math.Round(
                        trend.Sum(t => t.Income) /
                        monthsWithData,
                        2)
                    : 0,

            BestMonth =
                trend.Count > 0
                    ? trend
                        .OrderByDescending(t => t.Income)
                        .First()
                    : null,

            Sources = bySource,

            Trend = trend,

            RecordCount = all.Count
        };
    }

    // ------------------------------------------------------------------ annual summary

    public AnnualSummaryViewModel GetAnnualSummary(
        int userId,
        int year)
    {
        var months =
            new List<AnnualMonthRow>();

        for (var m = 1; m <= 12; m++)
        {
            months.Add(new AnnualMonthRow
            {
                Month = m,

                Label =
                    Format.MonthShort(m),

                Income =
                    _incomes.TotalForMonth(
                        userId,
                        year,
                        m),

                Expenses =
                    _expenses.TotalForMonth(
                        userId,
                        year,
                        m),

                Saved =
                    _goals.SavedInMonth(
                        userId,
                        year,
                        m)
            });
        }

        return new AnnualSummaryViewModel
        {
            Year = year,

            Currency =
                Currency(userId),

            Months = months,

            TopCategories =
                _expenses
                    .SpendingByCategoryForYear(
                        userId,
                        year,
                        5)
        };
    }

    // ------------------------------------------------------------------ custom date range

    /// <summary>
    /// Totals for any date range, computed directly from the transactions that
    /// fall inside it — no month/year assumptions, so the numbers stay accurate.
    /// </summary>
    public DateRangeViewModel GetDateRange(int userId, DateTime from, DateTime to)
    {
        // Normalize so the range is always ordered and inclusive.
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var incomeItems = _incomes.ForRange(userId, from, to);
        var expenseItems = _expenses.ForRange(userId, from, to);

        return new DateRangeViewModel
        {
            From = from.Date,
            To = to.Date,
            Currency = Currency(userId),

            TotalIncome = incomeItems.Sum(i => i.Amount),
            TotalExpenses = expenseItems.Sum(e => e.Amount),

            IncomeCount = incomeItems.Count,
            ExpenseCount = expenseItems.Count,

            // Goal contributions recorded inside the range.
            Saved = _goals.SavedInRange(userId, from, to),

            IncomeItems = incomeItems,
            ExpenseItems = expenseItems,

            // Category breakdown built only from expenses inside the range.
            TopCategories = expenseItems
                .GroupBy(e => e.Category)
                .Select(g => new CategorySlice
                {
                    Category = g.Key,
                    Amount = g.Sum(e => e.Amount),
                    Count = g.Count(),
                    Percent = expenseItems.Sum(e => e.Amount) > 0
                        ? Math.Round(g.Sum(e => e.Amount) / expenseItems.Sum(e => e.Amount) * 100, 1)
                        : 0
                })
                .OrderByDescending(x => x.Amount)
                .ToList()
        };
    }
}