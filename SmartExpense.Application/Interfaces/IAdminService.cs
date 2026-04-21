using SmartExpense.Application.Dtos.Auth;

namespace SmartExpense.Application.Interfaces;

public interface IAdminService
{
    Task<IEnumerable<UserWithRolesDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<UserWithRolesDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<BasicResponse> MakeUserAdminAsync(Guid userId, Guid currentAdminId,
        CancellationToken cancellationToken = default);

    Task<BasicResponse> RemoveAdminRoleAsync(Guid userId, Guid currentAdminId,
        CancellationToken cancellationToken = default);

    Task<BasicResponse> DeleteUserAsync(Guid userId, Guid currentAdminId,
        CancellationToken cancellationToken = default);
}