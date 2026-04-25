namespace SmartExpense.Application.Dtos.Auth;

public record RefreshTokenRequest
{
    public string? RefreshToken { get; init; }
}