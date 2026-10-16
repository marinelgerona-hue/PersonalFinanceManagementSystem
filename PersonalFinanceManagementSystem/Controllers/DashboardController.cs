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
        return View(_reports.GetDashboard(CurrentUser.Id));
    }
}
