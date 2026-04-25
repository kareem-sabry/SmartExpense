namespace SmartExpense.Application.Dtos.Auth;

public record UserWithRolesDto
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public List<string> Roles { get; init; } = new();
}