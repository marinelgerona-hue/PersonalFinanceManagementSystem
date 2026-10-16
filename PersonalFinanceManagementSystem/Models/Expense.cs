namespace PersonalFinanceManagementSystem.Models;

/// <summary>An expense record (category, amount, date, note).</summary>
public class Expense
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// Income pool (daily, weekly, monthly or yearly) this expense deducts
    /// from. Null for records created before the pool feature existed —
    /// they are counted once in overall totals but belong to no pool.
    /// </summary>
    public IncomePeriod? IncomePool { get; set; }

    /// <summary>
    /// Budget this expense is deducted from, when the user picked one at
    /// save time. Null expenses still count towards any matching budget of
    /// the same category whose period contains the expense date.
    /// </summary>
    public int? BudgetId { get; set; }
}
