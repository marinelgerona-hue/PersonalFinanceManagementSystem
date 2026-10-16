using Microsoft.EntityFrameworkCore;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Database tables
        public DbSet<User> Users { get; set; }
        public DbSet<Income> Incomes { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<SavingsGoal> SavingsGoals { get; set; }
        public DbSet<GoalContribution> GoalContributions { get; set; }
        public DbSet<RecurringTransaction> RecurringTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Income period enum stored as int (0=Daily, 1=Weekly, 2=Monthly, 3=Yearly)
            modelBuilder.Entity<Income>()
                .Property(i => i.Period)
                .HasConversion<int>();

            // Expense income pool: same enum, nullable so pre-pool expenses stay null
            modelBuilder.Entity<Expense>()
                .Property(e => e.IncomePool)
                .HasConversion<int?>();

            // User → Income
            modelBuilder.Entity<Income>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // User → Expense
            modelBuilder.Entity<Expense>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // User → Budget
            modelBuilder.Entity<Budget>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // User → SavingsGoal
            modelBuilder.Entity<SavingsGoal>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // User → GoalContribution
            modelBuilder.Entity<GoalContribution>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // SavingsGoal → GoalContribution
            modelBuilder.Entity<GoalContribution>()
                .HasOne<SavingsGoal>()
                .WithMany()
                .HasForeignKey(g => g.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            // RecurringTransaction enums stored as int
            modelBuilder.Entity<RecurringTransaction>()
                .Property(r => r.Type)
                .HasConversion<int>();
            modelBuilder.Entity<RecurringTransaction>()
                .Property(r => r.Frequency)
                .HasConversion<int>();
            modelBuilder.Entity<RecurringTransaction>()
                .Property(r => r.IncomePool)
                .HasConversion<int?>();

            // User → RecurringTransaction
            modelBuilder.Entity<RecurringTransaction>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // RecurringTransaction → generated Income/Expense rows: detach (don't
            // cascade-delete or block deletion of the definition) so history survives.
            modelBuilder.Entity<Income>()
                .HasOne<RecurringTransaction>()
                .WithMany()
                .HasForeignKey(i => i.RecurringTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Expense>()
                .HasOne<RecurringTransaction>()
                .WithMany()
                .HasForeignKey(e => e.RecurringTransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}