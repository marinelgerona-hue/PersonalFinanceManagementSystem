using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Areas.Admin.Controllers;

/// <summary>Admin Settings: the signed-in administrator's account details and password.</summary>
[Area("Admin")]
public class SettingsController : AdminControllerBase
{
    public SettingsController(IAuthService auth, IAdminService admin) : base(auth, admin) { }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Admin Settings";
        ViewData["ActiveNav"] = "settings";
        return View(BuildVm());
    }

    // ------------------------------------------------------------ profile

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveProfile(
        [Bind(Prefix = "Profile")] AdminSettingsFormViewModel profile)
    {
        var vm = BuildVm();

        if (!TryValidateModel(profile, nameof(vm.Profile)))
        {
            vm.Profile = profile;
            return View("Index", vm);
        }

        var error = Admin.UpdateAdminProfile(CurrentUser, profile, HttpContext);
        if (error.Length > 0)
        {
            vm.Profile = profile;
            vm.ProfileError = error;
            return View("Index", vm);
        }

        // Refresh the re-rendered identity from the database.
        var updated = Auth.GetUser(CurrentUser.Id);
        if (updated is not null) CurrentUser = ViewBag.User = updated;

        TempData["ToastTitle"] = "Profile saved";
        TempData["Success"] = "Your administrator account details were updated.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ password

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SavePassword(
        [Bind(Prefix = "ChangePassword")] AdminChangePasswordViewModel changePassword)
    {
        var vm = BuildVm();

        if (!TryValidateModel(changePassword, nameof(vm.ChangePassword)))
        {
            vm.ChangePassword = changePassword;
            return View("Index", vm);
        }

        if (!Admin.ChangeAdminPassword(CurrentUser, changePassword, HttpContext))
        {
            vm.ChangePassword = changePassword;
            vm.PasswordError = "Your current password is incorrect.";
            return View("Index", vm);
        }

        TempData["ToastTitle"] = "Password changed";
        TempData["Success"] = "Your administrator password was changed.";
        return RedirectToAction(nameof(Index));
    }

    private AdminSettingsViewModel BuildVm() => new()
    {
        Admin = CurrentUser,
        Profile = new AdminSettingsFormViewModel
        {
            FullName = CurrentUser.FullName,
            Email = CurrentUser.Email,
            Phone = CurrentUser.Phone
        }
    };
}
