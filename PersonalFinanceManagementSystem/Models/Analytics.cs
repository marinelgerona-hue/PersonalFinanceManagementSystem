namespace PersonalFinanceManagementSystem.Models;

/// <summary>One slice of a category breakdown (e.g. spending per category), with share of total.</summary>
public class CategorySlice
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Percent { get; set; }
    public int Count { get; set; }
    public decimal Average => Count > 0 ? Math.Round(Amount / Count, 2) : 0;
}

/// <summary>Income/expense totals for one month, used by trend charts.</summary>
public class MonthPoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net => Income - Expenses;
}

/// <summary>A single-month amount, used by contribution/income trends.</summary>
public class MonthAmount
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>A budget row together with its computed spending status.</summary>
public class BudgetStatus
{
    public Budget Budget { get; set; } = new();
    public decimal Spent { get; set; }
    public decimal Remaining { get; set; }
    public decimal PercentUsed { get; set; }
    public bool IsOver => PercentUsed >= 100;
    public bool IsNear => !IsOver && PercentUsed >= 80;
}

/// <summary>A savings goal together with its computed progress.</summary>
public class GoalProgress
{
    public SavingsGoal Goal { get; set; } = new();
    public decimal Percent { get; set; }
    public decimal Remaining { get; set; }
    public bool Completed => Saved >= Target;
    public decimal Saved => Goal.SavedAmount;
    public decimal Target => Goal.TargetAmount;

    /// <summary>Whether the deadline (if any) has already passed.</summary>
    public bool IsOverdue => Goal.Deadline is DateTime dl && !Completed && dl.Date < DateTime.Today;

    /// <summary>Whether the deadline is within 30 days and the goal isn't done.</summary>
    public bool IsDueSoon => Goal.Deadline is DateTime d2 && !Completed && !IsOverdue && d2.Date <= DateTime.Today.AddDays(30);

    // --------------------------------------------------- countdown + savings plan
    // All values below are computed live from Remaining and the deadline on
    // every read, so they follow goal contributions and target-date edits
    // automatically — there is no stored copy to fall out of sync.

    /// <summary>Whole days from today until the deadline (0 = today is the
    /// last day). Null when the goal has no deadline.</summary>
    public int? DaysLeft => Goal.Deadline is DateTime dl
        ? (dl.Date - DateTime.Today).Days
        : null;

    /// <summary>User-friendly countdown label for the goal card.</summary>
    public string CountdownLabel => DaysLeft is not int d ? string.Empty
        : d > 1 ? $"{d} days left"
        : d == 1 ? "1 day left"
        : d == 0 ? "Last day"
        : "Deadline passed";

    /// <summary>True when a daily/weekly/monthly savings plan can be shown:
    /// a deadline exists (today or later) and money is still missing.</summary>
    public bool HasPlan => Goal.Deadline is DateTime dl
        && dl.Date >= DateTime.Today
        && Remaining > 0;

    /// <summary>Amount to save per day until the deadline, with today
    /// counted as the first saving day. Null when no plan applies.</summary>
    public decimal? SavePerDay => !HasPlan ? null
        : Math.Max(1, (DaysLeft ?? 0) + 1) is int days
            ? Math.Round(Remaining / days, 2)
            : null;

    /// <summary>Amount to save per week until the deadline,
    /// never below one week. Null when no plan applies.</summary>
    public decimal? SavePerWeek => !HasPlan ? null
        : (decimal?)Math.Max(1M, Decimal.Ceiling(((DaysLeft ?? 0) + 1) / 7M)) is decimal weeks
            ? Math.Round(Remaining / weeks, 2)
            : null;

    /// <summary>Amount to save per month until the deadline,
    /// never below one month. Null when no plan applies.</summary>
    public decimal? SavePerMonth => !HasPlan ? null
        : Goal.Deadline is DateTime dl
            ? Math.Max(1, MonthsUntil(dl)) is int months
                ? Math.Round(Remaining / months, 2)
                : null
            : null;

    /// <summary>Whole calendar months from today to the deadline date.</summary>
    private static int MonthsUntil(DateTime deadline)
    {
        var today = DateTime.Today;
        var months = (deadline.Year - today.Year) * 12 + (deadline.Month - today.Month);
        if (deadline.Day < today.Day)
        {
            months -= 1;
        }
        return months;
    }

    /// <summary>User-facing status label for this goal.</summary>
    public string Status =>
        Completed ? "Completed"
        : IsOverdue ? "Overdue"
        : IsDueSoon ? "Due soon"
        : Saved == 0 ? "Not started"
        : "In progress";

    /// <summary>Chip css class for the status label.</summary>
    public string StatusClass => Status switch
    {
        "Completed" => "chip-success",
        "Overdue" => "chip-danger",
        "Due soon" => "chip-warn",
        "Not started" => "chip-muted",
        _ => "chip-info"
    };
}

/// <summary>An upcoming or overdue recurring bill/income, for the bell and dashboard widget.</summary>
public class UpcomingBill
{
    public RecurringTransaction Item { get; set; } = new();
    public bool IsOverdue => Item.NextDueDate.Date < DateTime.Today;
    public bool IsDueSoon => !IsOverdue && Item.NextDueDate.Date <= DateTime.Today.AddDays(7);
    public int? DaysUntil => IsOverdue ? null : (Item.NextDueDate.Date - DateTime.Today).Days;

    public string CountdownLabel => IsOverdue ? "Overdue"
        : DaysUntil == 0 ? "Due today"
        : DaysUntil == 1 ? "Due tomorrow"
        : $"Due in {DaysUntil} days";

    public string Status => IsOverdue ? "Overdue" : IsDueSoon ? "Due soon" : "Upcoming";

    public string StatusClass => Status switch
    {
        "Overdue" => "chip-danger",
        "Due soon" => "chip-warn",
        _ => "chip-info"
    };
}

/// <summary>A merged income/expense row for the "recent activity" list.</summary>
public class RecentTransaction
{
    public DateTime Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsIncome { get; set; }
}

/// <summary>One row of the annual summary table.</summary>
public class AnnualMonthRow
{
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net => Income - Expenses;
    public decimal Saved { get; set; }
}
