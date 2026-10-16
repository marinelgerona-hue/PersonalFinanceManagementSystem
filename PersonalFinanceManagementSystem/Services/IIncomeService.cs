using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

public interface IIncomeService
{
    List<Income> GetAll(int userId);
    Income? Get(int userId, int id);
    Income Add(Income income);
    /// <summary>Inserts several income records in a single round trip (e.g. CSV import).</summary>
    void AddRange(IEnumerable<Income> incomes);
    bool Update(Income income);
    bool Delete(int userId, int id);
    decimal Total(int userId);
    decimal TotalForMonth(int userId, int year, int month);
    List<Income> ForMonth(int userId, int year, int month);
    /// <summary>Income records for one period kind, newest first.</summary>
    List<Income> ForPeriod(int userId, IncomePeriod period);
    /// <summary>Total amounts grouped by income period (daily/weekly/monthly/yearly).</summary>
    List<PeriodSlice> TotalsByPeriod(int userId);
    /// <summary>Total income recorded in the last N days (rolling window).</summary>
    decimal TotalLastDays(int userId, int days);
    /// <summary>Total income for an inclusive date range.</summary>
    decimal TotalForRange(int userId, DateTime from, DateTime to);
    /// <summary>Income records in an inclusive date range, newest first.</summary>
    List<Income> ForRange(int userId, DateTime from, DateTime to);
}
