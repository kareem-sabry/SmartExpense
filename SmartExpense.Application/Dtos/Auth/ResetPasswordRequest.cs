using System.ComponentModel.DataAnnotations;
using SmartExpense.Core.Constants;

namespace SmartExpense.Application.Dtos.Auth;

public record ResetPasswordRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public required string Email { get; init; }

    [Required(ErrorMessage = "Reset code is required")]
    public required string ResetCode { get; init; }

    [Required(ErrorMessage = "New password is required")]
    [StringLength(ApplicationConstants.MaxPasswordLength, MinimumLength = ApplicationConstants.MinPasswordLength,
        ErrorMessage = "Password must be between 8 and 50 characters")]
    public required string NewPassword { get; init; }
}