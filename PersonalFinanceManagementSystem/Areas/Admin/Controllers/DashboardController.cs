using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Areas.Admin.Controllers;

/// <summary>Admin landing page: registered-user counts and headline system statistics.</summary>
[Area("Admin")]
public class DashboardController : AdminControllerBase
{
    public DashboardController(IAuthService auth, IAdminService admin) : base(auth, admin) { }

    public IActionResult Index()
    {
        var vm = Admin.GetStats();
        ViewData["Title"] = "Admin Dashboard";
        ViewData["ActiveNav"] = "dashboard";
        return View(vm);
    }
}
