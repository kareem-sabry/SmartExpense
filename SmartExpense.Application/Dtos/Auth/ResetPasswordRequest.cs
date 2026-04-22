namespace SmartExpense.Application.Dtos.Auth;

public record ResetPasswordRequest
{
    public required string Email { get; init; }
    public required string ResetCode { get; init; }
    public required string NewPassword { get; init; }
}