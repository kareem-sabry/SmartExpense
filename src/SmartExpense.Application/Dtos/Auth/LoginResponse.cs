namespace SmartExpense.Application.Dtos.Auth;

public record LoginResponse : BasicResponse
{
    public string? AccessToken { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public string? RefreshToken { get; init; }
}