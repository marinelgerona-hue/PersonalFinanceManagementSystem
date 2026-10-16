using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>Builds every dashboard/report view model from the live in-memory data.</summary>
public interface IReportService
{
    DashboardViewModel GetDashboard(int userId);
    MonthlySummaryViewModel GetMonthlySummary(int userId, int year, int month);
    ExpenseAnalysisViewModel GetExpenseAnalysis(int userId, int year, int month);
    SavingsReportViewModel GetSavingsReport(int userId);
    BudgetReportViewModel GetBudgetReport(int userId, int year, int month);
    IncomeReportViewModel GetIncomeReport(int userId);
    AnnualSummaryViewModel GetAnnualSummary(int userId, int year);
    /// <summary>Custom report: accurate income/expense/net totals for an inclusive date range.</summary>
    DateRangeViewModel GetDateRange(int userId, DateTime from, DateTime to);
}
