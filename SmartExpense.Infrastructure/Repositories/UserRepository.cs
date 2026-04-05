using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Infrastructure.Data;

namespace SmartExpense.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
    }

    public async Task<Dictionary<Guid, List<string>>> GetRolesByUserIdsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(
                _context.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, r.Name })
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(x => x.Name).OfType<string>().ToList(),
                cancellationToken);
    }
}