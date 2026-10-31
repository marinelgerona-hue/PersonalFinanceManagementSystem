using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Areas.Admin.Controllers;

/// <summary>
/// Category Management: the system-wide catalog of income sources and expense
/// categories that users pick from when adding records.
/// </summary>
[Area("Admin")]
public class CategoriesController : AdminControllerBase
{
    public CategoriesController(IAuthService auth, IAdminService admin) : base(auth, admin) { }

    // ------------------------------------------------------------ list

    public IActionResult Index(ListQuery query, CategoryType? type)
    {
        var vm = Admin.ListCategories(query, type);
        ViewData["Title"] = "Category Management";
        ViewData["ActiveNav"] = "categories";
        return View(vm);
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create(CategoryType? type)
    {
        var form = Admin.GetCategoryForm(0);
        if (type is not null) form.Type = type.Value;
        ViewData["Title"] = "New Category";
        ViewData["ActiveNav"] = "categories";
        return View("CategoryForm", form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(AdminCategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Category";
            ViewData["ActiveNav"] = "categories";
            return View("CategoryForm", model);
        }

        var (success, error) = Admin.CreateCategory(model, CurrentUser, HttpContext);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            ViewData["Title"] = "New Category";
            ViewData["ActiveNav"] = "categories";
            return View("CategoryForm", model);
        }

        TempData["ToastTitle"] = "Category created";
        TempData["Success"] = $"“{model.Name}” is available to users.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public IActionResult Edit(int id)
    {
        try
        {
            ViewData["Title"] = "Edit Category";
            ViewData["ActiveNav"] = "categories";
            return View("CategoryForm", Admin.GetCategoryForm(id));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, AdminCategoryFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Category";
            ViewData["ActiveNav"] = "categories";
            return View("CategoryForm", model);
        }

        var (success, error) = Admin.UpdateCategory(model, CurrentUser, HttpContext);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error);
            ViewData["Title"] = "Edit Category";
            ViewData["ActiveNav"] = "categories";
            return View("CategoryForm", model);
        }

        TempData["ToastTitle"] = "Category updated";
        TempData["Success"] = $"“{model.Name}” was saved.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ activate / deactivate

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Activate(int id, string? returnUrl)
    {
        var (success, error) = Admin.SetCategoryActive(id, true, CurrentUser, HttpContext);
        TempData[success ? "Success" : "Error"] = success ? "Category re-activated — users can pick it again." : error;
        return RedirectToReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id, string? returnUrl)
    {
        var (success, error) = Admin.SetCategoryActive(id, false, CurrentUser, HttpContext);
        TempData[success ? "Success" : "Error"] = success
            ? "Category deactivated — it no longer appears in user pickers."
            : error;
        return RedirectToReturnUrl(returnUrl);
    }

    // ------------------------------------------------------------ delete

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id, string? returnUrl)
    {
        var (success, error) = Admin.DeleteCategory(id, CurrentUser, HttpContext);
        TempData[success ? "Success" : "Error"] = success
            ? "Category deleted from the catalog."
            : error;
        if (!success) TempData["ToastTitle"] = "Not allowed";
        return RedirectToReturnUrl(returnUrl);
    }
}
