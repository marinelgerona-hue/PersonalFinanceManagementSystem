using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinanceManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class BudgetDateRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new schedule columns with a safe MySQL default first;
            // legacy rows are converted from Period/Year/Month/Day below,
            // then the legacy columns are dropped.
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Budgets",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "'2000-01-01'");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Budgets",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "'2000-01-01'");

            // Period stored as int (0=Monthly, 1=Daily, 2=Weekly, 3=Yearly):
            //   Monthly -> the whole anchor month
            //   Daily   -> the anchor day itself
            //   Weekly  -> the 7 days starting on the anchor day
            //   Yearly  -> the whole anchor year
            migrationBuilder.Sql(@"
UPDATE Budgets SET
  StartDate = CASE
    WHEN Period = 3 THEN CONCAT(Year, '-01-01')
    ELSE CONCAT(Year, '-', LPAD(Month, 2, '0'), '-', LPAD(Day, 2, '0'))
  END,
  EndDate = CASE
    WHEN Period = 3 THEN CONCAT(Year, '-12-31')
    WHEN Period = 1 THEN CONCAT(Year, '-', LPAD(Month, 2, '0'), '-', LPAD(Day, 2, '0'))
    WHEN Period = 2 THEN DATE_ADD(CONCAT(Year, '-', LPAD(Month, 2, '0'), '-', LPAD(Day, 2, '0')), INTERVAL 6 DAY)
    ELSE LAST_DAY(CONCAT(Year, '-', LPAD(Month, 2, '0'), '-01'))
  END;");

            migrationBuilder.DropColumn(
                name: "Day",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Month",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Budgets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old anchor columns cannot be reconstructed for every
            // custom schedule, so downgrades keep the Start Date's month as
            // a monthly anchor (Period 0 = Monthly).
            migrationBuilder.AddColumn<int>(
                name: "Day",
                table: "Budgets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Month",
                table: "Budgets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Period",
                table: "Budgets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Budgets",
                type: "int",
                nullable: false,
                defaultValue: 2000);

            migrationBuilder.Sql(@"
UPDATE Budgets SET
  Period = 0,
  Year = YEAR(StartDate),
  Month = MONTH(StartDate),
  Day = 1;");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Budgets");
        }
    }
}
