using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Areas.Admin.Controllers;

/// <summary>Activity Logs: the audit trail of important user and system events.</summary>
[Area("Admin")]
public class ActivityLogsController : AdminControllerBase
{
    public ActivityLogsController(IAuthService auth, IAdminService admin) : base(auth, admin) { }

    public IActionResult Index(ListQuery query, int user, string action)
    {
        var vm = Admin.ListActivity(query, user, action);
        ViewData["Title"] = "Activity Logs";
        ViewData["ActiveNav"] = "activity";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        Admin.ClearActivity(CurrentUser, HttpContext);
        TempData["ToastTitle"] = "Activity cleared";
        TempData["Success"] = "Every activity log entry was removed.";
        return RedirectToAction(nameof(Index));
    }
}
