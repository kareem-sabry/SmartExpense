using SmartExpense.Core.Entities;

namespace SmartExpense.Application.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Looks up a user by their current hashed refresh token.
    /// The caller must hash the plain incoming token before passing it here.
    /// </summary>
    Task<User?> GetUserByRefreshTokenAsync(string hashedToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a user by their PREVIOUS hashed refresh token.
    /// A match here indicates a rotated token was re-presented — possible theft.
    /// </summary>
    Task<User?> GetUserByPreviousRefreshTokenHashAsync(string hashedToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a dictionary mapping each user ID to their list of role names.
    ///     Executes a single JOIN query — avoids calling GetRolesAsync per user in a loop.
    /// </summary>
    Task<Dictionary<Guid, List<string>>> GetRolesByUserIdsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);
}