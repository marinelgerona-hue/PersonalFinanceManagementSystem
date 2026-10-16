using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>
/// Base controller for all pages that require a signed-in user.
/// Loads the current user into ViewBag.User (used by the layout shell),
/// or redirects to the login page when nobody is signed in.
/// </summary>
public abstract class AppControllerBase : Controller
{
    protected IAuthService Auth { get; }
    public User CurrentUser { get; protected set; } = new();

    protected AppControllerBase(IAuthService auth) => Auth = auth;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = Auth.GetCurrentUser(HttpContext);
        if (user is null)
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }

        CurrentUser = user;
        ViewBag.User = user;
        base.OnActionExecuting(context);
    }
}
