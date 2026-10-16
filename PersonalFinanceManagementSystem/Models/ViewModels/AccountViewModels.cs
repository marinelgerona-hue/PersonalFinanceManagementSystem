using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Keep the user signed in across browser restarts (30-day
    /// secure auth cookie). Unchecked = the usual session-only login.</summary>
    public bool RememberMe { get; set; }
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

    [StringLength(80, MinimumLength = 6,
        ErrorMessage = "Phone number must be exactly 11 digits.")]
    [DataType(DataType.PhoneNumber)]
    [Display(Name = "Phone (optional)")]
    public string? Phone { get; set; }

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
