using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
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

    /// <summary>
    /// Redirects back to <paramref name="returnUrl"/> — typically a hidden field carrying the
    /// exact filtered/sorted/paged list URL a form was rendered on — when it's a safe, local
    /// path, so adding funds, deleting a row etc. doesn't silently bounce the user back to
    /// page 1 of the default view. Falls back to the current controller's Index action.
    /// </summary>
    protected IActionResult RedirectToReturnUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index");

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
        ViewBag.Nav = BuildNavState(user);
        base.OnActionExecuting(context);
    }

    /// <summary>Budget/goal signals for the sidebar badge, budget meter and notification bell.
    /// Never lets a failure here break the page itself.</summary>
    private NavState BuildNavState(User user)
    {
        var now = DateTime.Now;
        var nav = new NavState { MonthLabel = now.ToString("MMMM"), Currency = user.Currency };
        try
        {
            var budgets = HttpContext.RequestServices.GetService<IBudgetService>()?
                .GetStatusForMonth(user.Id, now.Year, now.Month) ?? new();
            nav.MonthBudgeted = budgets.Sum(b => b.Budget.Limit);
            nav.MonthSpent = budgets.Sum(b => b.Spent);
            nav.BudgetsOver = budgets.Count(b => b.IsOver);

            foreach (var b in budgets.Where(b => b.IsOver).OrderByDescending(b => b.PercentUsed))
                nav.Alerts.Add(new NavAlert
                {
                    Level = "danger", Controller = "Budgets",
                    Title = $"{b.Budget.Category} is over budget",
                    Detail = $"{Format.Money0(b.Spent, user.Currency)} of {Format.Money0(b.Budget.Limit, user.Currency)} · over by {Format.Money0(Math.Abs(b.Remaining), user.Currency)}"
                });
            foreach (var b in budgets.Where(b => b.IsNear).OrderByDescending(b => b.PercentUsed))
                nav.Alerts.Add(new NavAlert
                {
                    Level = "warn", Controller = "Budgets",
                    Title = $"{b.Budget.Category} is at {Format.Pct(b.PercentUsed)}%",
                    Detail = $"{Format.Money0(b.Remaining, user.Currency)} left this month"
                });

            var goals = HttpContext.RequestServices.GetService<ISavingsGoalService>()?.GetProgress(user.Id) ?? new();
            foreach (var g in goals.Where(g => g.IsOverdue || g.IsDueSoon))
                nav.Alerts.Add(new NavAlert
                {
                    Level = g.IsOverdue ? "danger" : "info", Controller = "Goals",
                    Title = g.IsOverdue ? $"{g.Goal.Name} deadline passed" : $"{g.Goal.Name} is due soon",
                    Detail = $"{Format.Money0(g.Remaining, user.Currency)} still to save · {g.CountdownLabel}"
                });
        }
        catch
        {
            // The shell still renders without live signals.
        }
        return nav;
    }
}
