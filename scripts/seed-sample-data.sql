-- Realistic sample data for one Finance Management account (May – mid-October 2026).
-- Replaces that account's income, expenses, budgets, savings goals and recurring
-- transactions. Other accounts are untouched.
--
-- Usage (Docker MySQL from this repo's setup) — pick the account by email:
--   { echo "SET @email = 'you@example.com';"; cat scripts/seed-sample-data.sql; } \
--     | docker exec -i pfms-mysql mysql -uroot -p12345 PersonalFinanceManagementDb
-- Without @email it seeds demo.preview@example.com.

SET NAMES utf8mb4; -- notes contain em dashes; make sure they arrive as UTF-8
SET @email = COALESCE(@email, 'demo.preview@example.com');
SET @u = (SELECT Id FROM Users WHERE Email = @email);
SELECT IF(@u IS NULL, CONCAT('No account found for ', @email), CONCAT('Seeding account #', @u, ' (', @email, ')')) AS status;

START TRANSACTION;

-- Clear this account's existing finance data
DELETE c FROM GoalContributions c WHERE c.UserId = @u;
DELETE FROM SavingsGoals WHERE UserId = @u;
DELETE FROM RecurringTransactions WHERE UserId = @u;
DELETE FROM Expenses WHERE UserId = @u;
DELETE FROM Budgets WHERE UserId = @u;
DELETE FROM Incomes WHERE UserId = @u;

-- Income
INSERT INTO Incomes (UserId, Source, Amount, Date, Note, Period) VALUES
  (@u, 'Salary', 5450.00, '2026-05-01', 'Monthly salary — Northwind Analytics', 2),
  (@u, 'Freelance', 640.00, '2026-05-21', 'Dashboard prototype — Brightside Dental', 2),
  (@u, 'Salary', 5450.00, '2026-06-01', 'Monthly salary — Northwind Analytics', 2),
  (@u, 'Freelance', 1200.00, '2026-06-18', 'UX audit — Harbor & Pine Coffee', 2),
  (@u, 'Investments', 142.18, '2026-06-30', 'Quarterly dividend — total-market ETF', 2),
  (@u, 'Salary', 5450.00, '2026-07-01', 'Monthly salary — Northwind Analytics', 2),
  (@u, 'Bonus', 1500.00, '2026-07-15', 'Q2 performance bonus', 3),
  (@u, 'Salary', 5720.00, '2026-08-01', 'Monthly salary — Northwind Analytics (after raise)', 2),
  (@u, 'Freelance', 1850.00, '2026-08-22', 'Landing page redesign — Ridgeway Fitness', 2),
  (@u, 'Salary', 5720.00, '2026-09-01', 'Monthly salary — Northwind Analytics', 2),
  (@u, 'Gift', 200.00, '2026-09-12', 'Birthday gift from Mom & Dad', 2),
  (@u, 'Investments', 168.40, '2026-09-30', 'Quarterly dividend — total-market ETF', 2),
  (@u, 'Salary', 5720.00, '2026-10-01', 'Monthly salary — Northwind Analytics', 2),
  (@u, 'Freelance', 720.00, '2026-10-09', 'Analytics consulting — 6 hours', 2);

-- Budgets (start/end date schedules, September and October)
INSERT INTO Budgets (UserId, Category, `Limit`, StartDate, EndDate) VALUES
  (@u, 'Groceries', 650.00, '2026-09-01', '2026-09-30'),
  (@u, 'Dining', 250.00, '2026-09-01', '2026-09-30'),
  (@u, 'Transportation', 220.00, '2026-09-01', '2026-09-30'),
  (@u, 'Entertainment', 120.00, '2026-09-01', '2026-09-30'),
  (@u, 'Shopping', 200.00, '2026-09-01', '2026-09-30'),
  (@u, 'Utilities', 330.00, '2026-09-01', '2026-09-30'),
  (@u, 'Groceries', 650.00, '2026-10-01', '2026-10-31'),
  (@u, 'Dining', 250.00, '2026-10-01', '2026-10-31'),
  (@u, 'Transportation', 220.00, '2026-10-01', '2026-10-31'),
  (@u, 'Entertainment', 120.00, '2026-10-01', '2026-10-31'),
  (@u, 'Shopping', 200.00, '2026-10-01', '2026-10-31'),
  (@u, 'Utilities', 330.00, '2026-10-01', '2026-10-31');

-- Expenses (all deducted from the Monthly income period)
INSERT INTO Expenses (UserId, Category, Amount, Date, Note, IncomePool) VALUES
  (@u, 'Housing', 1850.00, '2026-05-01', 'Rent — Maple Court apartment', 2),
  (@u, 'Groceries', 142.37, '2026-05-03', 'Costco — monthly stock-up', 2),
  (@u, 'Utilities', 69.99, '2026-05-04', 'Home internet — Comcast', 2),
  (@u, 'Health', 49.00, '2026-05-05', 'Gym membership — Equinox', 2),
  (@u, 'Utilities', 45.00, '2026-05-06', 'Mobile phone plan', 2),
  (@u, 'Transportation', 52.30, '2026-05-07', 'Fuel — Shell', 2),
  (@u, 'Entertainment', 11.99, '2026-05-08', 'Spotify Premium', 2),
  (@u, 'Entertainment', 15.49, '2026-05-08', 'Netflix subscription', 2),
  (@u, 'Dining', 38.60, '2026-05-09', 'Lunch with team — Sweetgreen', 2),
  (@u, 'Groceries', 86.42, '2026-05-10', 'Trader Joe''s', 2),
  (@u, 'Shopping', 46.89, '2026-05-12', 'Amazon — household supplies', 2),
  (@u, 'Utilities', 96.40, '2026-05-12', 'Electricity — PG&E', 2),
  (@u, 'Transportation', 23.60, '2026-05-14', 'Uber to airport', 2),
  (@u, 'Dining', 72.40, '2026-05-16', 'Dinner — Nobu Sushi', 2),
  (@u, 'Groceries', 63.18, '2026-05-17', 'Whole Foods', 2),
  (@u, 'Entertainment', 32.00, '2026-05-19', 'Movie night — AMC', 2),
  (@u, 'Transportation', 48.75, '2026-05-21', 'Fuel — Chevron', 2),
  (@u, 'Dining', 14.85, '2026-05-24', 'Chipotle', 2),
  (@u, 'Groceries', 91.05, '2026-05-26', 'Trader Joe''s', 2),
  (@u, 'Shopping', 89.00, '2026-05-28', 'Running shoes — Nike', 2),
  (@u, 'Housing', 1850.00, '2026-06-01', 'Rent — Maple Court apartment', 2),
  (@u, 'Groceries', 156.80, '2026-06-02', 'Costco — monthly stock-up', 2),
  (@u, 'Utilities', 69.99, '2026-06-04', 'Home internet — Comcast', 2),
  (@u, 'Health', 49.00, '2026-06-05', 'Gym membership — Equinox', 2),
  (@u, 'Transportation', 55.10, '2026-06-05', 'Fuel — Shell', 2),
  (@u, 'Dining', 46.20, '2026-06-06', 'Brunch — The Daily Grind', 2),
  (@u, 'Utilities', 45.00, '2026-06-06', 'Mobile phone plan', 2),
  (@u, 'Entertainment', 11.99, '2026-06-08', 'Spotify Premium', 2),
  (@u, 'Entertainment', 15.49, '2026-06-08', 'Netflix subscription', 2),
  (@u, 'Groceries', 78.15, '2026-06-09', 'Trader Joe''s', 2),
  (@u, 'Education', 18.99, '2026-06-11', 'Online course — SQL for analysts', 2),
  (@u, 'Utilities', 118.25, '2026-06-12', 'Electricity — PG&E', 2),
  (@u, 'Dining', 18.40, '2026-06-13', 'Thai takeout', 2),
  (@u, 'Groceries', 54.92, '2026-06-16', 'Farmers market', 2),
  (@u, 'Transportation', 50.85, '2026-06-19', 'Fuel — Chevron', 2),
  (@u, 'Shopping', 64.50, '2026-06-20', 'Amazon — kitchen essentials', 2),
  (@u, 'Groceries', 97.34, '2026-06-23', 'Whole Foods', 2),
  (@u, 'Health', 28.40, '2026-06-24', 'Pharmacy — CVS', 2),
  (@u, 'Dining', 64.75, '2026-06-27', 'Birthday dinner — Olive & Ivy', 2),
  (@u, 'Housing', 1850.00, '2026-07-01', 'Rent — Maple Court apartment', 2),
  (@u, 'Groceries', 138.66, '2026-07-02', 'Costco — monthly stock-up', 2),
  (@u, 'Travel', 386.40, '2026-07-03', 'Flights to Denver — Southwest', 2),
  (@u, 'Utilities', 69.99, '2026-07-04', 'Home internet — Comcast', 2),
  (@u, 'Health', 49.00, '2026-07-05', 'Gym membership — Equinox', 2),
  (@u, 'Transportation', 49.95, '2026-07-06', 'Fuel — Shell', 2),
  (@u, 'Utilities', 45.00, '2026-07-06', 'Mobile phone plan', 2),
  (@u, 'Dining', 22.15, '2026-07-08', 'Tacos & Co.', 2),
  (@u, 'Entertainment', 11.99, '2026-07-08', 'Spotify Premium', 2),
  (@u, 'Entertainment', 15.49, '2026-07-08', 'Netflix subscription', 2),
  (@u, 'Groceries', 82.47, '2026-07-09', 'Trader Joe''s', 2),
  (@u, 'Utilities', 142.80, '2026-07-12', 'Electricity — PG&E', 2),
  (@u, 'Shopping', 129.99, '2026-07-14', 'Carry-on suitcase — Samsonite', 2),
  (@u, 'Travel', 512.00, '2026-07-18', 'Hotel, 3 nights — Denver', 2),
  (@u, 'Dining', 84.60, '2026-07-19', 'Dinner in Denver', 2),
  (@u, 'Travel', 96.80, '2026-07-19', 'Rental car — Denver', 2),
  (@u, 'Groceries', 71.30, '2026-07-23', 'Whole Foods', 2),
  (@u, 'Transportation', 18.00, '2026-07-25', 'Parking — downtown', 2),
  (@u, 'Dining', 31.90, '2026-07-26', 'Pizza night — Domino''s', 2),
  (@u, 'Housing', 1850.00, '2026-08-01', 'Rent — Maple Court apartment', 2),
  (@u, 'Groceries', 149.21, '2026-08-02', 'Costco — monthly stock-up', 2),
  (@u, 'Transportation', 53.40, '2026-08-04', 'Fuel — Shell', 2),
  (@u, 'Utilities', 69.99, '2026-08-04', 'Home internet — Comcast', 2),
  (@u, 'Health', 49.00, '2026-08-05', 'Gym membership — Equinox', 2),
  (@u, 'Utilities', 45.00, '2026-08-06', 'Mobile phone plan', 2),
  (@u, 'Dining', 41.30, '2026-08-07', 'Lunch — Sweetgreen', 2),
  (@u, 'Entertainment', 11.99, '2026-08-08', 'Spotify Premium', 2),
  (@u, 'Entertainment', 15.49, '2026-08-08', 'Netflix subscription', 2),
  (@u, 'Groceries', 88.73, '2026-08-10', 'Trader Joe''s', 2),
  (@u, 'Shopping', 212.45, '2026-08-12', 'Back-to-office clothes — Uniqlo', 2),
  (@u, 'Utilities', 151.10, '2026-08-12', 'Electricity — PG&E', 2),
  (@u, 'Dining', 58.25, '2026-08-15', 'Dinner — Lucia Trattoria', 2),
  (@u, 'Groceries', 66.04, '2026-08-17', 'Whole Foods', 2),
  (@u, 'Health', 180.00, '2026-08-19', 'Dentist — cleaning & check-up', 2),
  (@u, 'Transportation', 51.15, '2026-08-20', 'Fuel — Chevron', 2),
  (@u, 'Entertainment', 64.00, '2026-08-23', 'Concert tickets', 2),
  (@u, 'Groceries', 79.58, '2026-08-27', 'Trader Joe''s', 2),
  (@u, 'Education', 24.99, '2026-08-29', 'Book — Storytelling with Data', 2),
  (@u, 'Groceries', 161.92, '2026-09-01', 'Costco — monthly stock-up', 2),
  (@u, 'Housing', 1850.00, '2026-09-01', 'Rent — Maple Court apartment', 2),
  (@u, 'Transportation', 54.20, '2026-09-03', 'Fuel — Shell', 2),
  (@u, 'Utilities', 69.99, '2026-09-04', 'Home internet — Comcast', 2),
  (@u, 'Health', 49.00, '2026-09-05', 'Gym membership — Equinox', 2),
  (@u, 'Dining', 36.80, '2026-09-06', 'Brunch — The Daily Grind', 2),
  (@u, 'Utilities', 45.00, '2026-09-06', 'Mobile phone plan', 2),
  (@u, 'Entertainment', 11.99, '2026-09-08', 'Spotify Premium', 2),
  (@u, 'Entertainment', 15.49, '2026-09-08', 'Netflix subscription', 2),
  (@u, 'Groceries', 74.38, '2026-09-08', 'Trader Joe''s', 2),
  (@u, 'Transportation', 129.00, '2026-09-10', 'Car service — oil change & inspection', 2),
  (@u, 'Utilities', 112.35, '2026-09-12', 'Electricity — PG&E', 2),
  (@u, 'Dining', 92.50, '2026-09-13', 'Birthday dinner — Ember Steakhouse', 2),
  (@u, 'Groceries', 92.11, '2026-09-15', 'Whole Foods', 2),
  (@u, 'Transportation', 47.90, '2026-09-17', 'Fuel — Chevron', 2),
  (@u, 'Shopping', 58.30, '2026-09-18', 'Amazon — desk lamp & cables', 2),
  (@u, 'Groceries', 68.47, '2026-09-22', 'Farmers market', 2),
  (@u, 'Dining', 16.45, '2026-09-24', 'Chipotle', 2),
  (@u, 'Entertainment', 28.00, '2026-09-27', 'Bowling with friends', 2),
  (@u, 'Housing', 1850.00, '2026-10-01', 'Rent — Maple Court apartment', 2),
  (@u, 'Groceries', 168.54, '2026-10-02', 'Costco — monthly stock-up', 2),
  (@u, 'Dining', 48.90, '2026-10-03', 'Lunch with client — Sweetgreen', 2),
  (@u, 'Utilities', 69.99, '2026-10-04', 'Home internet — Comcast', 2),
  (@u, 'Health', 49.00, '2026-10-05', 'Gym membership — Equinox', 2),
  (@u, 'Transportation', 51.80, '2026-10-05', 'Fuel — Shell', 2),
  (@u, 'Utilities', 45.00, '2026-10-06', 'Mobile phone plan', 2),
  (@u, 'Groceries', 94.21, '2026-10-07', 'Trader Joe''s', 2),
  (@u, 'Entertainment', 11.99, '2026-10-08', 'Spotify Premium', 2),
  (@u, 'Entertainment', 15.49, '2026-10-08', 'Netflix subscription', 2),
  (@u, 'Dining', 118.75, '2026-10-09', 'Anniversary dinner — Aria', 2),
  (@u, 'Shopping', 74.99, '2026-10-10', 'Rain jacket — Patagonia', 2),
  (@u, 'Groceries', 112.68, '2026-10-11', 'Whole Foods', 2),
  (@u, 'Dining', 34.60, '2026-10-12', 'Ramen — Mensho', 2),
  (@u, 'Utilities', 88.60, '2026-10-12', 'Electricity — PG&E', 2),
  (@u, 'Transportation', 26.40, '2026-10-13', 'Uber — downtown', 2),
  (@u, 'Dining', 62.30, '2026-10-14', 'Team happy hour', 2),
  (@u, 'Entertainment', 36.00, '2026-10-15', 'Movie night — AMC', 2),
  (@u, 'Groceries', 178.40, '2026-10-15', 'Costco — hosting dinner party', 2);

-- Link September/October expenses to the matching category budget
UPDATE Expenses e JOIN Budgets b
  ON b.UserId = e.UserId AND b.Category = e.Category
 AND e.Date BETWEEN b.StartDate AND b.EndDate
SET e.BudgetId = b.Id
WHERE e.UserId = @u;

-- Savings goals with their contribution history (SavedAmount = sum of contributions)
INSERT INTO SavingsGoals (UserId, Name, TargetAmount, SavedAmount, Deadline, Note) VALUES (@u, 'Emergency fund', 15000.00, 8500.00, '2027-06-30', 'Six months of essential expenses.');
SET @g = LAST_INSERT_ID();
INSERT INTO GoalContributions (GoalId, UserId, Amount, Date, Note) VALUES
  (@g, @u, 6000.00, '2026-05-02', 'Opening balance moved from checking'),
  (@g, @u, 500.00, '2026-06-02', 'Monthly auto-transfer'),
  (@g, @u, 500.00, '2026-07-02', 'Monthly auto-transfer'),
  (@g, @u, 500.00, '2026-08-02', 'Monthly auto-transfer'),
  (@g, @u, 500.00, '2026-09-02', 'Monthly auto-transfer'),
  (@g, @u, 500.00, '2026-10-02', 'Monthly auto-transfer');

INSERT INTO SavingsGoals (UserId, Name, TargetAmount, SavedAmount, Deadline, Note) VALUES (@u, 'Japan trip — spring 2027', 4500.00, 1900.00, '2027-04-15', 'Flights, rail pass and two weeks of hotels.');
SET @g = LAST_INSERT_ID();
INSERT INTO GoalContributions (GoalId, UserId, Amount, Date, Note) VALUES
  (@g, @u, 300.00, '2026-06-03', 'Monthly transfer'),
  (@g, @u, 300.00, '2026-07-03', 'Monthly transfer'),
  (@g, @u, 300.00, '2026-08-03', 'Monthly transfer'),
  (@g, @u, 300.00, '2026-09-03', 'Monthly transfer'),
  (@g, @u, 300.00, '2026-10-03', 'Monthly transfer'),
  (@g, @u, 400.00, '2026-08-23', 'Part of freelance payment');

INSERT INTO SavingsGoals (UserId, Name, TargetAmount, SavedAmount, Deadline, Note) VALUES (@u, 'New laptop', 2400.00, 2400.00, '2026-09-30', 'Replace the 2019 laptop for freelance work.');
SET @g = LAST_INSERT_ID();
INSERT INTO GoalContributions (GoalId, UserId, Amount, Date, Note) VALUES
  (@g, @u, 600.00, '2026-05-22', 'Freelance payment'),
  (@g, @u, 800.00, '2026-06-19', 'Freelance payment'),
  (@g, @u, 1000.00, '2026-07-16', 'Part of Q2 bonus');

INSERT INTO SavingsGoals (UserId, Name, TargetAmount, SavedAmount, Deadline, Note) VALUES (@u, 'Car down payment', 8000.00, 1200.00, '2028-01-31', '20% down on a used hybrid.');
SET @g = LAST_INSERT_ID();
INSERT INTO GoalContributions (GoalId, UserId, Amount, Date, Note) VALUES
  (@g, @u, 400.00, '2026-08-05', 'Monthly transfer'),
  (@g, @u, 400.00, '2026-09-05', 'Monthly transfer'),
  (@g, @u, 400.00, '2026-10-05', 'Monthly transfer');

-- Recurring transactions (Type: 0=Income, 1=Expense · Frequency: 0=Daily, 1=Weekly,
-- 2=Biweekly, 3=Monthly, 4=Yearly · IncomePool reuses the Income period enum:
-- 0=Daily, 1=Weekly, 2=Monthly, 3=Yearly). Dates are chosen relative to "today"
-- in this sample world (mid-October 2026) to show every state the feature has:
-- one item already overdue, two due within the next two weeks (one of them an
-- income), a yearly bill, and one paused item.
INSERT INTO RecurringTransactions (UserId, Type, Label, Amount, Frequency, StartDate, NextDueDate, EndDate, IsActive, Note, IncomePool, BudgetId) VALUES
  -- Rent, due the 1st of each month — next occurrence is November.
  (@u, 1, 'Housing', 1850.00, 3, '2026-05-01', '2026-11-01', NULL, 1, 'Rent — Maple Court apartment', 2, NULL),
  -- Home internet, due the 4th — next occurrence is November.
  (@u, 1, 'Utilities', 69.99, 3, '2026-05-04', '2026-11-04', NULL, 1, 'Home internet — Comcast', 2, NULL),
  -- Netflix, due the 8th — next occurrence is November.
  (@u, 1, 'Entertainment', 15.49, 3, '2026-05-08', '2026-11-08', NULL, 1, 'Netflix subscription', 2, NULL),
  -- Gym membership, billed on the 30th — due in ~2 weeks (shows as "due soon").
  (@u, 1, 'Health', 49.00, 3, '2026-05-30', '2026-10-30', NULL, 1, 'Gym membership — Equinox', 2, NULL),
  -- Small cloud storage subscription, billed on the 15th — overdue by 2 days,
  -- so the first page load after seeding demonstrates catch-up generation.
  (@u, 1, 'Entertainment', 2.99, 3, '2026-05-15', '2026-10-15', NULL, 1, 'iCloud+ 200GB storage', 2, NULL),
  -- Car insurance, once a year — next renewal is months away.
  (@u, 1, 'Transportation', 780.00, 4, '2026-03-01', '2027-03-01', NULL, 1, 'Car insurance — annual premium', 2, NULL),
  -- Personal website domain, once a year — renews in 3 days (shows as "due soon").
  (@u, 1, 'Other', 14.00, 4, '2025-10-20', '2026-10-20', NULL, 1, 'Domain renewal — personal website', 2, NULL),
  -- Pet insurance — paused (coverage on hold), so it never generates or alerts.
  (@u, 1, 'Shopping', 35.00, 3, '2026-06-01', '2026-11-01', NULL, 0, 'Pet insurance — Mittens (on hold)', 2, NULL),
  -- Weekend tutoring, weekly income — next session is in a week (shows as "due soon").
  (@u, 0, 'Freelance', 120.00, 1, '2026-09-05', '2026-10-24', NULL, 1, 'Tutoring — weekend sessions', NULL, NULL),
  -- Part-time contract, paid every two weeks — next payment is in ~2 weeks.
  (@u, 0, 'Freelance', 850.00, 2, '2026-10-02', '2026-10-30', NULL, 1, 'Part-time contract — Riverside Design Co.', NULL, NULL);

COMMIT;

SELECT
  (SELECT COUNT(*) FROM Incomes WHERE UserId = @u) AS incomes,
  (SELECT COUNT(*) FROM Expenses WHERE UserId = @u) AS expenses,
  (SELECT COUNT(*) FROM Budgets WHERE UserId = @u) AS budgets,
  (SELECT COUNT(*) FROM SavingsGoals WHERE UserId = @u) AS goals,
  (SELECT COUNT(*) FROM GoalContributions WHERE UserId = @u) AS contributions,
  (SELECT COUNT(*) FROM RecurringTransactions WHERE UserId = @u) AS recurring_transactions;
