namespace PersonalFinanceManagementSystem.Models;

/// <summary>How often a recurring transaction repeats.</summary>
public enum RecurrenceFrequency
{
    Daily = 0,
    Weekly = 1,
    Biweekly = 2,
    Monthly = 3,
    Yearly = 4
}

/// <summary>Which table a generated occurrence lands in.</summary>
public enum RecurringTransactionType
{
    Income = 0,
    Expense = 1
}

/// <summary>
/// A recurring bill/income definition. Due occurrences are generated as real
/// Income/Expense rows on the next authenticated page load (see
/// RecurringTransactionService.GenerateDueOccurrences), tagged back to this
/// definition via RecurringTransactionId.
/// </summary>
public class RecurringTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Which table a generated occurrence is written into.</summary>
    public RecurringTransactionType Type { get; set; }

    /// <summary>Expense.Category or Income.Source, depending on Type.</summary>
    public string Label { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>First occurrence date. Immutable anchor for day-of-month math —
    /// never mutated after creation, so NextDueDate can drift (e.g. Feb clamp)
    /// without ever corrupting the target day for later months.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Date of the next occurrence still to be generated — the sole
    /// scheduling cursor. Advanced whenever an occurrence is generated or skipped.</summary>
    public DateTime NextDueDate { get; set; }

    /// <summary>Optional stop condition — no occurrences generated after this date.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Pause without deleting. Paused items are skipped by
    /// GenerateDueOccurrences/GetUpcoming; past generated rows are kept.</summary>
    public bool IsActive { get; set; } = true;

    public string Note { get; set; } = string.Empty;

    /// <summary>Mirrors Expense.IncomePool — Type == Expense only; copied onto
    /// every generated Expense row.</summary>
    public IncomePeriod? IncomePool { get; set; }

    /// <summary>Legacy budget linkage retained for database compatibility. New
    /// recurring transactions and generated expenses do not use this field.</summary>
    public int? BudgetId { get; set; }
}
