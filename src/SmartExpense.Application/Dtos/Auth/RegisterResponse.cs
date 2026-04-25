namespace SmartExpense.Application.Dtos.Auth;

public record RegisterResponse : BasicResponse
{
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();
}