using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Areas.Admin.Controllers;

/// <summary>
/// User Management: view, search, create, edit, activate, deactivate and
/// delete accounts. Financial record contents stay private — only counts and
/// account-level metadata are shown here.
/// </summary>
[Area("Admin")]
public class UsersController : AdminControllerBase
{
    public UsersController(IAuthService auth, IAdminService admin) : base(auth, admin) { }

    // ------------------------------------------------------------ list

    public IActionResult Index(ListQuery query, string? role, string? status)
    {
        var filter = new AdminUserFilter(role, status);
        var vm = Admin.ListUsers(query, filter);
        ViewData["Title"] = "User Management";
        ViewData["ActiveNav"] = "users";
        return View(vm);
    }

    // ------------------------------------------------------------ details

    [HttpGet]
    public IActionResult Details(int id)
    {
        try
        {
            var vm = Admin.GetUserDetails(CurrentUser, id);
            ViewData["Title"] = $"Account · {vm.User.FullName}";
            ViewData["ActiveNav"] = "users";
            return View(vm);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create Account";
        ViewData["ActiveNav"] = "users";
        return View("UserForm", Admin.GetUserForm(0));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(AdminUserFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Create Account";
            ViewData["ActiveNav"] = "users";
            return View("UserForm", model);
        }

        var (success, error) = Admin.CreateUser(model, CurrentUser, HttpContext);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            ViewData["Title"] = "Create Account";
            ViewData["ActiveNav"] = "users";
            return View("UserForm", model);
        }

        TempData["ToastTitle"] = "Account created";
        TempData["Success"] = $"{model.FullName} can now sign in";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public IActionResult Edit(int id)
    {
        try
        {
            ViewData["Title"] = "Edit Account";
            ViewData["ActiveNav"] = "users";
            return View("UserForm", Admin.GetUserForm(id));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, AdminUserFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Account";
            ViewData["ActiveNav"] = "users";
            return View("UserForm", model);
        }

        var (success, error) = Admin.UpdateUser(model, CurrentUser, HttpContext);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            ViewData["Title"] = "Edit Account";
            ViewData["ActiveNav"] = "users";
            return View("UserForm", model);
        }

        TempData["ToastTitle"] = "Account updated";
        TempData["Success"] = $"{model.FullName}'s account details were saved.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ activate / deactivate

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Activate(int id, string? returnUrl)
    {
        var (success, error) = Admin.SetActive(id, true, CurrentUser, HttpContext);
        TempData[success ? "Success" : "Error"] = success ? "Account activated — it can sign in again." : error;
        if (!success) TempData["ToastTitle"] = "Not allowed";
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id, string? returnUrl)
    {
        var (success, error) = Admin.SetActive(id, false, CurrentUser, HttpContext);
        TempData[success ? "Success" : "Error"] = success
            ? "Account deactivated — it can no longer sign in."
            : error;
        if (!success) TempData["ToastTitle"] = "Not allowed";
        return RedirectToReturnUrl(returnUrl);
    }

    // ------------------------------------------------------------ delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id, string? returnUrl)
    {
        var (success, error) = Admin.Delete(id, CurrentUser, HttpContext);
        TempData[success ? "Success" : "Error"] = success
            ? "The account and its financial records were deleted."
            : error;
        if (!success) TempData["ToastTitle"] = "Not allowed";
        if (success && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToReturnUrl(returnUrl);
    }
}
