using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SmartExpense.Core.Entities;

public class User : IdentityUser<Guid>
{
    [Required] [MaxLength(100)] public required string FirstName { get; set; }

    [Required] [MaxLength(100)] public required string LastName { get; set; }


    [MaxLength(88)] public string? RefreshToken { get; set; }

    // Holds the hash of the immediately previous refresh token.
    // A match here signals token reuse (theft) rather than a normal invalid request.
    [MaxLength(88)] public string? PreviousRefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<Category> Categories { get; set; } = new List<Category>();

    public static User Create(string email, string firstName, string lastName, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name cannot be empty", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name cannot be empty", nameof(lastName));

        return new User
        {
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        };
    }

    public override string ToString()
    {
        return $"{FirstName} {LastName}";
    }
}