using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

public class HomeController : Controller
{
    private readonly IAuthService _auth;

    public HomeController(IAuthService auth) => _auth = auth;

    /// <summary>Sends signed-in users to the dashboard; visitors see the welcome landing page.</summary>
    public IActionResult Index()
    {
        if (_auth.GetCurrentUser(HttpContext) is not null)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
