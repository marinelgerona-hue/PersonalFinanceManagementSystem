using System.ComponentModel.DataAnnotations;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

public class ExpenseFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please choose a category.")]
    public string Category { get; set; } = string.Empty;

    /// <summary>Custom category typed by the user when Category is
    /// "Other". Only read when Category == "Other" (validated in the
    /// controller); empty otherwise.</summary>
    [StringLength(100, ErrorMessage = "Custom category must be 100 characters or fewer.")]
    public string? CustomCategory { get; set; }

    [Required(ErrorMessage = "Please enter an amount.")]
    [Range(0.01, 99_999_999, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Please choose a date.")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(300)]
    public string? Note { get; set; }

    /// <summary>Budget the expense is deducted from. Optional — null counts
    /// against whichever matching budget's period covers the expense date.</summary>
    public int? BudgetId { get; set; }

    /// <summary>Income pool (daily, weekly, monthly or yearly) the expense
    /// deducts from. Required with a Monthly default — pre-pool records
    /// carry no pool and are only counted in overall totals.</summary>
    [Display(Name = "Income period to deduct from")]
    public IncomePeriod IncomePool { get; set; } = IncomePeriod.Monthly;

    /// <summary>Actual income and pool-expense records loaded from the
    /// database so the browser can show the live available balance as the
    /// pool/expense date change. Expense rows carry ExpenseId so an edited
    /// record's old deduction can be excluded.</summary>
    public List<PoolLedgerEntry> PoolLedger { get; set; } = new();

    /// <summary>The signed-in user's budgets to pick from in the expense form.
    /// Each option carries its category and period window so the browser can
    /// filter the list as category/date change.</summary>
    public List<BudgetOption> Budgets { get; set; } = new();
}

public class ExpenseListViewModel
{
    /// <summary>The current page of the (searched, sorted) expense table.</summary>
    public PagedList<Expense> Page { get; set; } = new();
    public ListQuery Query { get; set; } = new();

    /// <summary>All of the user's expense records, regardless of search or paging.</summary>
    public int RecordCount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal MonthTotal { get; set; }
    public decimal Total { get; set; }
    public int ThisMonthCount { get; set; }

    /// <summary>All-time income, so spending can be seen against earnings.</summary>
    public decimal TotalIncome { get; set; }

    /// <summary>Budget names keyed by budget id, used to show which budget
    /// each expense record deducts from.</summary>
    public Dictionary<int, string> BudgetNames { get; set; } = new();

    /// <summary>TotalIncome − Total. Recomputed on every page load, so it always
    /// reflects the latest income and expense records.</summary>
    public decimal RemainingBalance => TotalIncome - Total;
}

/// <summary>One ledger row loaded from the database for income-pool balance
/// math in the expense form. Income rows credit a pool (their own income
/// period); pooled expense rows debit one. Rows dated after the expense's
/// cutoff do not count toward that expense's available balance.</summary>
public class PoolLedgerEntry
{
    /// <summary>"income" or "expense".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The income period an income row belongs to, or the pool an
    /// expense row draws from.</summary>
    public IncomePeriod Period { get; set; }

    public DateTime Date { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Id of the expense row the ledger entry came from. The form
    /// excludes its own edited record so the shown balance is the balance
    /// available *before* this expense is applied.</summary>
    public int ExpenseId { get; set; }
}
