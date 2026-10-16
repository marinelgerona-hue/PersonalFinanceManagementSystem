using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Settings — view and update basic user information and password.</summary>
public class SettingsController : AppControllerBase
{
    /// <summary>Maximum accepted profile picture size (2 MB once Base64-encoded).</summary>
    private const long MaxPhotoLength = 2_000_000;

    private static readonly string[] AllowedPhotoTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };

    /// <summary>Name prefix of the change-password form fields.</summary>
    public const string PasswordFormPrefix = "changePassword";

    public SettingsController(IAuthService auth) : base(auth) { }

    public IActionResult Index()
    {
        SetPageInfo();
        return View(BuildModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateProfile(SettingsViewModel model, string? returnTab)
    {
        SetPageInfo();

        if (!ModelState.IsValid)
        {
            return View("Index", WithAccountInfo(model));
        }

        var error = Auth.UpdateProfile(CurrentUser.Id, model.FullName, model.Email, model.FullPhone, model.Currency);
        if (error.Length > 0)
        {
            ModelState.AddModelError(nameof(model.Email), error);
            return View("Index", WithAccountInfo(model));
        }

        RefreshCurrentUser();
        TempData["ToastTitle"] = "Settings saved";
        TempData["Success"] = "Your profile and preferences are up to date.";
        return RedirectToTab(returnTab == "preferences" ? "preferences" : "profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // The form's fields are named "changePassword.*" (see Views/Settings/Index.cshtml), so bind with that prefix.
    public IActionResult ChangePassword([Bind(Prefix = PasswordFormPrefix)] ChangePasswordViewModel model)
    {
        SetPageInfo();

        if (!ModelState.IsValid)
        {
            return View("Index", BuildModel());
        }

        if (!Auth.ChangePassword(CurrentUser.Id, model.CurrentPassword, model.NewPassword))
        {
            ModelState.AddModelError($"{PasswordFormPrefix}.{nameof(model.CurrentPassword)}", "Your current password is incorrect.");
            return View("Index", BuildModel());
        }

        TempData["ToastTitle"] = "Password updated";
        TempData["Success"] = "Use your new password the next time you sign in.";
        return RedirectToTab("change-password");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxPhotoLength + 65_000)] // headroom for multipart/base64 encoding
    public IActionResult UploadPhoto(IFormFile? photo)
    {
        if (photo is null || photo.Length == 0)
        {
            TempData["Error"] = "Please choose an image file first.";
            return RedirectToAction(nameof(Index));
        }

        if (photo.Length > MaxPhotoLength)
        {
            TempData["Error"] = "Image is too large. Please choose a picture under 2 MB.";
            return RedirectToAction(nameof(Index));
        }

        var contentType = (photo.ContentType ?? "").ToLowerInvariant();
        if (!AllowedPhotoTypes.Contains(contentType))
        {
            TempData["Error"] = "Unsupported image type. Please use JPG, PNG, GIF or WebP.";
            return RedirectToAction(nameof(Index));
        }

        using var ms = new MemoryStream();
        photo.CopyTo(ms);
        var dataUri = $"data:{contentType};base64,{Convert.ToBase64String(ms.ToArray())}";

        Auth.UpdateProfileImage(CurrentUser.Id, dataUri);
        RefreshCurrentUser();

        TempData["Success"] = "Profile picture updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemovePhoto()
    {
        Auth.UpdateProfileImage(CurrentUser.Id, null);
        RefreshCurrentUser();

        TempData["Success"] = "Profile picture removed.";
        return RedirectToAction(nameof(Index));
    }

    private void SetPageInfo()
    {
        ViewData["Title"] = "Settings";
        ViewData["Subtitle"] = "Manage your profile, preferences and account security.";
        ViewData["ActiveNav"] = "settings";
    }

    /// <summary>The settings form pre-filled from the signed-in user.</summary>
    private SettingsViewModel BuildModel()
    {
        var (iso, national) = Catalog.SplitPhone(CurrentUser.Phone);
        return WithAccountInfo(new SettingsViewModel
        {
            FullName = CurrentUser.FullName,
            Email = CurrentUser.Email,
            PhoneCountry = iso,
            Phone = national,
            Currency = CurrentUser.Currency
        });
    }

    /// <summary>Fills the read-only account details that aren't posted back with the form.</summary>
    private SettingsViewModel WithAccountInfo(SettingsViewModel model)
    {
        model.ProfileImage = CurrentUser.ProfileImage;
        model.MemberSince = CurrentUser.CreatedAt;
        return model;
    }

    /// <summary>Back to the settings page with the given tab open (tabs are selected by URL fragment).</summary>
    private IActionResult RedirectToTab(string tab) => RedirectToAction(nameof(Index), null, null, tab);

    /// <summary>Reloads the signed-in user so the layout shows fresh data.</summary>
    private void RefreshCurrentUser()
    {
        var refreshed = Auth.GetCurrentUser(HttpContext);
        if (refreshed is not null)
        {
            CurrentUser = refreshed;
            ViewBag.User = refreshed;
        }
    }
}
