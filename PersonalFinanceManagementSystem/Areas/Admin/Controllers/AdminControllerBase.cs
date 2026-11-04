using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Areas.Admin.Controllers;

/// <summary>
/// Base controller for every page inside the Admin area. It enforces the
/// role-based access control: no session → the login page; signed in as a
/// regular user → back to their dashboard with an explanation, and the admin
/// pages are never rendered for them.
/// </summary>
public abstract class AdminControllerBase : Controller
{
    protected IAuthService Auth { get; }
    protected IAdminService Admin { get; }
    public User CurrentUser { get; protected set; } = new();

    protected AdminControllerBase(IAuthService auth, IAdminService admin)
    {
        Auth = auth;
        Admin = admin;
    }

    protected IActionResult RedirectToReturnUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index");

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = Auth.GetCurrentUser(HttpContext);

        // Not signed in at all → the shared login page. "area = "" clears the
        // ambient Admin area value so the redirect doesn't stay inside /Admin.
        if (user is null)
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
            return;
        }

        // Signed in as a regular user → no admin access; explain and redirect
        // to their own dashboard (outside the Admin area).
        if (user.Role != UserRoles.Admin)
        {
            TempData["ToastTitle"] = "Access restricted";
            TempData["Error"] = "You need an administrator account to open that page.";
            context.Result = new RedirectToActionResult("Index", "Dashboard", new { area = "" });
            return;
        }

        CurrentUser = user;
        ViewBag.User = user;

        // Admin pages must never be restored from the browser cache after logout.
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        base.OnActionExecuting(context);
    }
}
