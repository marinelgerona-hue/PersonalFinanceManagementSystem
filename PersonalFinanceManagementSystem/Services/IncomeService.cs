using Microsoft.EntityFrameworkCore;
using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// CRUD + aggregation for income records using the MySQL database.
/// </summary>
public class IncomeService : IIncomeService
{
    private readonly ApplicationDbContext _db;

    public IncomeService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------
    // Get all income records for the current user
    // ------------------------------------------------------------

    public List<Income> GetAll(int userId)
    {
        return _db.Incomes
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Date)
            .ThenByDescending(i => i.Id)
            .ToList();
    }

    // ------------------------------------------------------------
    // Get one income record
    // ------------------------------------------------------------

    public Income? Get(int userId, int id)
    {
        return _db.Incomes
            .FirstOrDefault(i =>
                i.UserId == userId &&
                i.Id == id);
    }

    // ------------------------------------------------------------
    // Add income
    // ------------------------------------------------------------

    public Income Add(Income income)
    {
        _db.Incomes.Add(income);

        _db.SaveChanges();

        return income;
    }

    // ------------------------------------------------------------
    // Update income
    // ------------------------------------------------------------

    public bool Update(Income income)
    {
        var existing = _db.Incomes
            .FirstOrDefault(i =>
                i.UserId == income.UserId &&
                i.Id == income.Id);

        if (existing is null)
        {
            return false;
        }

        existing.Source = income.Source;
        existing.Amount = income.Amount;
        existing.Period = income.Period;
        existing.Date = income.Date;
        existing.Note = income.Note ?? string.Empty;

        _db.SaveChanges();

        return true;
    }

    // ------------------------------------------------------------
    // Delete income
    // ------------------------------------------------------------

    public bool Delete(int userId, int id)
    {
        var existing = _db.Incomes
            .FirstOrDefault(i =>
                i.UserId == userId &&
                i.Id == id);

        if (existing is null)
        {
            return false;
        }

        _db.Incomes.Remove(existing);

        _db.SaveChanges();

        return true;
    }

    // ------------------------------------------------------------
    // Total income
    // ------------------------------------------------------------

    public decimal Total(int userId)
    {
        return _db.Incomes
            .Where(i => i.UserId == userId)
            .Sum(i => i.Amount);
    }

    // ------------------------------------------------------------
    // Total income for a specific month
    // ------------------------------------------------------------

    public decimal TotalForMonth(
        int userId,
        int year,
        int month)
    {
        return _db.Incomes
            .Where(i =>
                i.UserId == userId &&
                i.Date.Year == year &&
                i.Date.Month == month)
            .Sum(i => i.Amount);
    }

    // ------------------------------------------------------------
    // Get income records for a specific month
    // ------------------------------------------------------------

    public List<Income> ForMonth(
        int userId,
        int year,
        int month)
    {
        return _db.Incomes
            .Where(i =>
                i.UserId == userId &&
                i.Date.Year == year &&
                i.Date.Month == month)
            .OrderByDescending(i => i.Date)
            .ToList();
    }

    // ------------------------------------------------------------
    // Income records for one period kind
    // ------------------------------------------------------------

    public List<Income> ForPeriod(int userId, IncomePeriod period)
    {
        return _db.Incomes
            .Where(i => i.UserId == userId && i.Period == period)
            .OrderByDescending(i => i.Date)
            .ThenByDescending(i => i.Id)
            .ToList();
    }

    // ------------------------------------------------------------
    // Totals grouped by income period
    // ------------------------------------------------------------

    public List<PeriodSlice> TotalsByPeriod(int userId)
    {
        var groups = _db.Incomes
            .Where(i => i.UserId == userId)
            .GroupBy(i => i.Period)
            .Select(g => new { Period = g.Key, Total = g.Sum(i => i.Amount), Count = g.Count() })
            .ToList();

        var total = groups.Sum(g => g.Total);

        return groups
            .Select(g => new PeriodSlice
            {
                Period = g.Period,
                Label = g.Period.ToString(),
                Tint = g.Period switch
                {
                    IncomePeriod.Daily => "tint-sky",
                    IncomePeriod.Weekly => "tint-amber",
                    IncomePeriod.Yearly => "tint-indigo",
                    _ => "tint-green"
                },
                Color = Format.CategoryColor(g.Period.ToString()),
                Total = g.Total,
                Count = g.Count,
                // Share of all income recorded for this period kind.
                Percent = total > 0 ? Math.Round(g.Total / total * 100, 1) : 0
            })
            .OrderBy(x => x.Period)
            .ToList();
    }

    // ------------------------------------------------------------
    // Total income in a rolling window of days
    // ------------------------------------------------------------

    public decimal TotalLastDays(int userId, int days)
    {
        var from = DateTime.Today.AddDays(-(days - 1));

        return _db.Incomes
            .Where(i =>
                i.UserId == userId &&
                i.Date >= from &&
                i.Date <= DateTime.Today)
            .Sum(i => i.Amount);
    }

    // ------------------------------------------------------------
    // Total income for an inclusive date range
    // ------------------------------------------------------------

    public decimal TotalForRange(int userId, DateTime from, DateTime to)
    {
        var toExclusive = to.Date.AddDays(1);

        return _db.Incomes
            .Where(i =>
                i.UserId == userId &&
                i.Date >= from.Date &&
                i.Date < toExclusive)
            .Sum(i => i.Amount);
    }

    // ------------------------------------------------------------
    // Income records in an inclusive date range
    // ------------------------------------------------------------

    public List<Income> ForRange(int userId, DateTime from, DateTime to)
    {
        var toExclusive = to.Date.AddDays(1);

        return _db.Incomes
            .Where(i =>
                i.UserId == userId &&
                i.Date >= from.Date &&
                i.Date < toExclusive)
            .OrderByDescending(i => i.Date)
            .ThenByDescending(i => i.Id)
            .ToList();
    }
}