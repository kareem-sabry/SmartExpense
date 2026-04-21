using System.ComponentModel.DataAnnotations;

namespace SmartExpense.Application.Dtos.Auth;

public record ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public required string Email { get; init; }
}