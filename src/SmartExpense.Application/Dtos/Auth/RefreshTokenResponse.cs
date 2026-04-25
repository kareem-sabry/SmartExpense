namespace SmartExpense.Application.Dtos.Auth;

public record RefreshTokenResponse : BasicResponse
{
    public string? AccessToken { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public string? RefreshToken { get; init; }
}