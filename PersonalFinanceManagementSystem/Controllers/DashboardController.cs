using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

public class DashboardController : AppControllerBase
{
    private readonly IReportService _reports;

    public DashboardController(IAuthService auth, IReportService reports) : base(auth)
        => _reports = reports;

    public IActionResult Index()
    {
        ViewData["Title"] = "Dashboard";
        ViewData["ActiveNav"] = "dashboard";

        var vm = _reports.GetDashboard(CurrentUser.Id);
        vm.FirstName = CurrentUser.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
        ViewData["Subtitle"] = $"{greeting}, {vm.FirstName} — here's where your money stands in {vm.MonthLabel}.";
        return View(vm);
    }
}
