using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>
/// Create account, login and logout.
/// </summary>
public class AccountController : Controller
{
    private readonly IAuthService _auth;

    public AccountController(IAuthService auth)
    {
        _auth = auth;
    }

    // ------------------------------------------------------------
    // LOGIN - GET
    // ------------------------------------------------------------

    [HttpGet]
    public IActionResult Login()
    {
        // If the user is already logged in,
        // don't show the login page again.
        if (_auth.GetCurrentUser(HttpContext) is not null)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel());
    }

    // ------------------------------------------------------------
    // LOGIN - POST
    // ------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        // Check validation
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Check email and password from the database
        var (success, error) =
            _auth.Login(
                model.Email,
                model.Password,
                model.RememberMe,
                HttpContext);

        // Login failed
        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                error);

            return View(model);
        }

        // Login successful.
        // Admins land in the Admin Console; regular users stay on their dashboard.
        var signedIn = _auth.GetCurrentUser(HttpContext);
        if (signedIn is not null && signedIn.Role == UserRoles.Admin)
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        // The dashboard greets the user, so no toast is needed here.
        return RedirectToAction(
            "Index",
            "Dashboard");
    }

    // ------------------------------------------------------------
    // REGISTER / CREATE ACCOUNT - GET
    // ------------------------------------------------------------

    [HttpGet]
    public IActionResult Register()
    {
        // If already logged in, go to Dashboard
        if (_auth.GetCurrentUser(HttpContext) is not null)
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }

        return View(new RegisterViewModel());
    }

    // ------------------------------------------------------------
    // REGISTER / CREATE ACCOUNT - POST
    // ------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
    {
        // Check validation
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Create the account and save it to MySQL
        var (success, error) =
            _auth.Register(
                model,
                HttpContext);

        // Registration failed
        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                error);

            return View(model);
        }

        // Registration successful.
        // The user is NOT automatically logged in.
        TempData["ToastTitle"] = "Account created";
        TempData["Success"] =
            "Sign in with your new email and password.";

        // Send the user to the Login page
        return RedirectToAction(
            "Login",
            "Account");
    }

    // ------------------------------------------------------------
    // LOGOUT
    // ------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _auth.Logout(HttpContext);

        TempData["ToastTitle"] = "Signed out";
        TempData["Success"] =
            "You've been signed out securely.";

        return RedirectToAction("Login");
    }
}