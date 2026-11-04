using System.ComponentModel.DataAnnotations;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

public class LoginViewModel
{
    /// <summary>Requested sign-in area: User or Admin.</summary>
    public string Role { get; set; } = UserRoles.User;

    [Required, EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Keep the user signed in across browser restarts (30-day
    /// secure auth cookie). Unchecked = the usual session-only login.</summary>
    public bool RememberMe { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Registered email")]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.User;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = UserRoles.User;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyOtpViewModel
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = UserRoles.User;

    [Required(ErrorMessage = "Enter the six-digit code from your email.")]
    [RegularExpression("^\\d{6}$", ErrorMessage = "The verification code must be six digits.")]
    [Display(Name = "Verification code")]
    public string Otp { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>ISO code of the country picked for the phone number (e.g. "US").</summary>
    [Display(Name = "Country code")]
    public string PhoneCountry { get; set; } = "US";

    /// <summary>The national part of the number, without the country code.
    /// Checked against the picked country (PH keeps the strict PH mobile rules).</summary>
    [PhoneNational]
    [DataType(DataType.PhoneNumber)]
    [Display(Name = "Phone (optional)")]
    public string? Phone { get; set; }

    /// <summary>Dial code + national digits (e.g. "+91 9876543210"), or empty when no number was entered.</summary>
    public string FullPhone => Catalog.ComposePhone(PhoneCountry, Phone);

    [Required]
    public string Currency { get; set; } = "USD";

    [Required(ErrorMessage = "Password is required.")]
    [MinLength
        (6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
