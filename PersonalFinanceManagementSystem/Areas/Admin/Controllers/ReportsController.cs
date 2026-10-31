using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Areas.Admin.Controllers;

/// <summary>System Reports: overall system-wide statistics with an Excel export.</summary>
[Area("Admin")]
public class ReportsController : AdminControllerBase
{
    public ReportsController(IAuthService auth, IAdminService admin) : base(auth, admin) { }

    public IActionResult Index()
    {
        var vm = Admin.GetReport();
        ViewData["Title"] = "System Reports";
        ViewData["ActiveNav"] = "reports";
        return View(vm);
    }

    /// <summary>Downloads the whole system report as an Excel workbook.</summary>
    [HttpGet]
    public IActionResult Export()
    {
        var vm = Admin.GetReport();

        var xlsx = new XlsxFile(Format.Symbol("USD"), new[] { "Report section", "Metric", "Value" },
            XlsxColumn.Text, XlsxColumn.Text, XlsxColumn.Text);

        void AddLines(string section, params (string Metric, object Value)[] rows)
        {
            foreach (var (metric, value) in rows) xlsx.AddRow(section, metric, value);
        }

        AddLines("Users",
            ("Total registered users", vm.TotalUsers),
            ("Administrators", vm.TotalAdmins),
            ("Active accounts", vm.ActiveUsers),
            ("Deactivated accounts", vm.InactiveUsers),
            ("New users this month", vm.NewUsersThisMonth));

        AddLines("Stored records",
            ("Income records", vm.TotalIncomeRecords),
            ("Expense records", vm.TotalExpenseRecords),
            ("Budgets", vm.TotalBudgets),
            ("Savings goals", vm.TotalGoals),
            ("Recurring definitions", vm.TotalRecurringDefinitions));

        AddLines("Activity audit",
            ("Total log entries", vm.TotalActivityEntries),
            ("Entries today", vm.ActivityEntriesToday));

        xlsx.AddSection(new[] { "Month", "New users", "Total users (to date)" },
            XlsxColumn.Text, XlsxColumn.Integer, XlsxColumn.Integer);
        foreach (var g in vm.UserGrowth)
            xlsx.AddRow($"{Format.MonthName(g.Month)} {g.Year}", g.NewUsers, g.TotalUsersToDate);

        return xlsx.ToFileResult($"system-report-{DateTime.Today:yyyy-MM-dd}.xlsx");
    }
}
