using Microsoft.AspNetCore.Mvc;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>Settings — view and update basic user information and password.</summary>
public class SettingsController : AppControllerBase
{
    /// <summary>Maximum accepted profile picture size (1 MB once Base64-encoded).</summary>
    private const long MaxPhotoLength = 1_000_000;

    private static readonly string[] AllowedPhotoTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };

    public SettingsController(IAuthService auth) : base(auth) { }

    public IActionResult Index()
    {
        ViewData["Title"] = "Settings";
        ViewData["ActiveNav"] = "settings";
        return View(new SettingsViewModel
        {
            FullName = CurrentUser.FullName,
            Email = CurrentUser.Email,
            Phone = CurrentUser.Phone,
            Currency = CurrentUser.Currency,
            ProfileImage = CurrentUser.ProfileImage,
            MemberSince = CurrentUser.CreatedAt
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateProfile(SettingsViewModel model)
    {
        ViewData["Title"] = "Settings";
        ViewData["ActiveNav"] = "settings";

        if (!ModelState.IsValid)
        {
            model.MemberSince = CurrentUser.CreatedAt;
            return View("Index", model);
        }

        var error = Auth.UpdateProfile(CurrentUser.Id, model.FullName, model.Email, model.Phone ?? "", model.Currency);
        if (error.Length > 0)
        {
            ModelState.AddModelError(nameof(model.Email), error);
            model.MemberSince = CurrentUser.CreatedAt;
            return View("Index", model);
        }

        // Refresh ViewBag user after the profile change.
        var updated = Auth.GetCurrentUser(HttpContext);
        if (updated is not null)
        {
            CurrentUser = updated;
            ViewBag.User = updated;
        }

        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(ChangePasswordViewModel model)
    {
        ViewData["Title"] = "Settings";
        ViewData["ActiveNav"] = "settings";

        if (!ModelState.IsValid)
        {
            return View("Index", new SettingsViewModel
            {
                FullName = CurrentUser.FullName,
                Email = CurrentUser.Email,
                Phone = CurrentUser.Phone,
                Currency = CurrentUser.Currency,
                MemberSince = CurrentUser.CreatedAt
            });
        }

        var ok = Auth.ChangePassword(CurrentUser.Id, model.CurrentPassword, model.NewPassword);
        if (!ok)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Your current password is incorrect.");
            return View("Index", new SettingsViewModel
            {
                FullName = CurrentUser.FullName,
                Email = CurrentUser.Email,
                Phone = CurrentUser.Phone,
                Currency = CurrentUser.Currency,
                MemberSince = CurrentUser.CreatedAt
            });
        }

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxPhotoLength + 65_000)] // headroom for multipart/base64 encoding
    public IActionResult UploadPhoto(IFormFile? photo)
    {
        ViewData["Title"] = "Settings";
        ViewData["ActiveNav"] = "settings";

        if (photo is null || photo.Length == 0)
        {
            TempData["Error"] = "Please choose an image file first.";
            return RedirectToAction(nameof(Index));
        }

        if (photo.Length > MaxPhotoLength)
        {
            TempData["Error"] = "Image is too large. Please choose a picture under 1 MB.";
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
