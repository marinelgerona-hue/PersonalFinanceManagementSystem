using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// Creates one demo account (demo@finapp.com / Demo@123) with 12 months of
/// income, expenses, budgets, goals and contributions. All dashboard/report
/// statistics are still computed live from these records — nothing is hardcoded.
/// </summary>
public static class DemoSeeder
{
    public static void Seed(InMemoryDataStore store)
    {
        var rnd = new Random(20260915); // fixed seed => stable demo data
        var today = DateTime.Today;

        var user = new User
        {
            Id = store.NextId(),
            FullName = "Alex Morgan",
            Email = "demo@finapp.com",
            Phone = "+1 555 010 2030",
            Currency = "USD",
            CreatedAt = today.AddYears(-1)
        };
        user.PasswordSalt = SecurityHelper.NewSalt();
        user.PasswordHash = SecurityHelper.Hash("Demo@123", user.PasswordSalt);
        store.Users.Add(user);

        int incomeId() => store.NextId();
        int expenseId() => store.NextId();
        int budgetId() => store.NextId();
        int goalId() => store.NextId();
        int contributionId() => store.NextId();

        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        for (var i = 0; i < 12; i++)
        {
            var monthStart = firstMonth.AddMonths(i);
            var lastDay = i == 11
                ? today.Day
                : DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

            DateTime Day(int d) => monthStart.AddDays(Math.Min(d, lastDay) - 1);

            // ---- Income ----
            store.Incomes.Add(new Income { Id = incomeId(), UserId = user.Id, Source = "Salary", Amount = 4200m, Date = Day(1), Note = "Monthly salary" });
            if (rnd.Next(100) < 55)
            {
                store.Incomes.Add(new Income { Id = incomeId(), UserId = user.Id, Source = "Freelance", Amount = rnd.Next(280, 960) + rnd.Next(0, 99) / 100m, Date = Day(rnd.Next(5, 26)), Note = "Side project" });
            }
            if (monthStart.Month == 12)
            {
                store.Incomes.Add(new Income { Id = incomeId(), UserId = user.Id, Source = "Bonus", Amount = 1500m, Date = Day(20), Note = "Year-end bonus" });
            }
            if (rnd.Next(100) < 25)
            {
                store.Incomes.Add(new Income { Id = incomeId(), UserId = user.Id, Source = "Investments", Amount = rnd.Next(40, 220) + rnd.Next(0, 99) / 100m, Date = Day(rnd.Next(2, 27)), Note = "Dividends" });
            }

            // ---- Expenses ----
            void AddExp(string category, decimal amount, int day, string note)
                => store.Expenses.Add(new Expense { Id = expenseId(), UserId = user.Id, Category = category, Amount = amount, Date = Day(day), Note = note });

            AddExp("Housing", 1150m, 2, "Apartment rent");

            var groceryTrips = rnd.Next(3, 5);
            for (var g = 0; g < groceryTrips; g++)
                AddExp("Groceries", rnd.Next(85, 165) + rnd.Next(0, 99) / 100m, 3 + g * 7, "Groceries");

            AddExp("Utilities", rnd.Next(85, 180) + rnd.Next(0, 99) / 100m, 10, "Electricity & water");

            var transport = rnd.Next(2, 4);
            for (var t = 0; t < transport; t++)
                AddExp("Transportation", rnd.Next(25, 75) + rnd.Next(0, 99) / 100m, 4 + t * 9, "Fuel & transit");

            var dining = rnd.Next(2, 5);
            for (var d = 0; d < dining; d++)
                AddExp("Dining", rnd.Next(18, 66) + rnd.Next(0, 99) / 100m, rnd.Next(3, 28), "Restaurants & coffee");

            if (rnd.Next(100) < 80)
                AddExp("Entertainment", rnd.Next(12, 60) + rnd.Next(0, 99) / 100m, rnd.Next(4, 27), "Movies & streaming");

            if (rnd.Next(100) < 55)
                AddExp("Shopping", rnd.Next(30, 220) + rnd.Next(0, 99) / 100m, rnd.Next(5, 26), "Online order");

            if (rnd.Next(100) < 25)
                AddExp("Health", rnd.Next(40, 180) + rnd.Next(0, 99) / 100m, rnd.Next(6, 24), "Pharmacy / checkup");
        }

        // ---- Budgets (current calendar month) ----
        var budgets = new (string Category, decimal Limit)[]
        {
            ("Housing", 1200m),
            ("Groceries", 500m),
            ("Transportation", 180m),
            ("Dining", 200m),
            ("Entertainment", 75m),
            ("Shopping", 280m)
        };
        foreach (var (category, limit) in budgets)
        {
            store.Budgets.Add(new Budget { Id = budgetId(), UserId = user.Id, Category = category, Limit = limit,
                StartDate = new DateTime(today.Year, today.Month, 1).Date,
                EndDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)).Date });
        }

        // ---- Savings goals + contribution history ----
        var goals = new (string Name, decimal Target, decimal Saved, DateTime? Deadline, string Note)[]
        {
            ("Emergency Fund", 10000m, 4250m, null, "Rainy-day savings"),
            ("New Laptop", 2499m, 820m, today.AddMonths(6), "MacBook Pro"),
            ("Vacation", 5000m, 1540m, today.AddMonths(9), "Trip to Bali"),
            ("Car Down Payment", 8000m, 2600m, today.AddMonths(18), "Used car fund")
        };

        foreach (var (name, target, saved, deadline, note) in goals)
        {
            var goal = new SavingsGoal { Id = goalId(), UserId = user.Id, Name = name, TargetAmount = target, SavedAmount = 0m, Deadline = deadline, Note = note };
            store.Goals.Add(goal);

            // Build the saved amount from 3 contributions over the past 5 months.
            var remaining = saved;
            for (var k = 0; k < 3; k++)
            {
                var amount = k == 2 ? remaining : Math.Round(saved / 3m, 2);
                remaining -= amount;
                store.Contributions.Add(new GoalContribution
                {
                    Id = contributionId(),
                    GoalId = goal.Id,
                    UserId = user.Id,
                    Amount = amount,
                    Date = today.AddMonths(-(5 - k)).AddDays(rnd.Next(0, 20)),
                    Note = "Transfer to savings"
                });
                goal.SavedAmount += amount;
            }
        }
    }
}
