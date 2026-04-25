namespace SmartExpense.Application.Dtos.Auth;

public record BasicResponse
{
    public bool Succeeded { get; init; }
    public string Message { get; init; }
}