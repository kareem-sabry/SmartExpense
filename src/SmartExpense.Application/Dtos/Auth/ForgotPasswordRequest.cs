namespace SmartExpense.Application.Dtos.Auth;

public record ForgotPasswordRequest
{
    public required string Email { get; init; }
}