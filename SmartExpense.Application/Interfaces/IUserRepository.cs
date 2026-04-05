using SmartExpense.Core.Entities;

namespace SmartExpense.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    /// <summary>
    ///     Returns a dictionary mapping each user ID to their list of role names.
    ///     Executes a single JOIN query — avoids calling GetRolesAsync per user in a loop.
    /// </summary>
    Task<Dictionary<Guid, List<string>>> GetRolesByUserIdsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);
}