using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Exceptions;
using ValidationException = SmartExpense.Core.Exceptions.ValidationException;

namespace SmartExpense.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ILogger<AdminService> _logger;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<User> _userManager;

    public AdminService(UserManager<User> userManager, ILogger<AdminService> logger,
        RoleManager<IdentityRole<Guid>> roleManager, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _logger = logger;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<UserWithRolesDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var rolesByUser = await _unitOfWork.Users.GetRolesByUserIdsAsync(userIds, cancellationToken);

        return users.Select(user => new UserWithRolesDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = rolesByUser.GetValueOrDefault(user.Id) ?? []
        });
    }

    public async Task<UserWithRolesDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateGuid(userId, nameof(userId));
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            throw new NotFoundException("User", userId);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserWithRolesDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles.ToList()
        };
    }

    public async Task<BasicResponse> MakeUserAdminAsync(Guid userId, string currentAdminEmail, CancellationToken cancellationToken = default)    {
        ValidateGuid(userId, nameof(userId));

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            throw new NotFoundException("User", userId);
        }

        if (user.Email?.Equals(currentAdminEmail, StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning("Admin {Email} attempted to modify own role", currentAdminEmail);

            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.CannotModifyOwnAccount
            };
        }

        if (await _userManager.IsInRoleAsync(user, IdentityRoleConstants.Admin))
        {
            _logger.LogInformation("User {Email} is already an admin", user.Email);

            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.UserAlreadyAdmin
            };
        }

        var result = await _userManager.AddToRoleAsync(user, IdentityRoleConstants.Admin);

        if (!result.Succeeded)
        {
            _logger.LogError("Failed to grant admin role to {Email}: {Errors}",
                user.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return new BasicResponse
            {
                Succeeded = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        _logger.LogInformation("Admin role granted to user {Email} by {AdminEmail}",
            user.Email, currentAdminEmail);

        return new BasicResponse
        {
            Succeeded = true,
            Message = $"{SuccessMessages.AdminRoleGranted} User: {user.Email}"
        };
    }

    public async Task<BasicResponse> RemoveAdminRoleAsync(Guid userId, Guid currentAdminId, CancellationToken cancellationToken = default)
    {
        ValidateGuid(userId, nameof(userId));
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            throw new NotFoundException("User", userId);
        }

        if (user.Id == currentAdminId)
        {
            _logger.LogWarning("Admin {UserId} attempted to remove own admin role", currentAdminId);

            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.CannotModifyOwnAccount
            };
        }

        if (!await _userManager.IsInRoleAsync(user, IdentityRoleConstants.Admin))
        {
            _logger.LogInformation("User {Email} is not an admin", user.Email);

            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.UserNotAdmin
            };
        }

        var result = await _userManager.RemoveFromRoleAsync(user, IdentityRoleConstants.Admin);

        if (!result.Succeeded)
        {
            _logger.LogError("Failed to remove admin role from {Email}: {Errors}",
                user.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return new BasicResponse
            {
                Succeeded = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        if (!await _userManager.IsInRoleAsync(user, IdentityRoleConstants.User))
        {
            var addResult = await _userManager.AddToRoleAsync(user, IdentityRoleConstants.User);
            if (!addResult.Succeeded)
            {
                _logger.LogError("Failed to add User role to {Email}: {Errors}",
                    user.Email,
                    string.Join(", ", addResult.Errors.Select(e => e.Description)));

                return new BasicResponse
                {
                    Succeeded = false,
                    Message = string.Join(", ", addResult.Errors.Select(e => e.Description))
                };
            }
        }

        _logger.LogInformation("Admin role removed from user {Email}", user.Email);

        return new BasicResponse
        {
            Succeeded = true,
            Message = $"{SuccessMessages.AdminRoleRemoved} User: {user.Email}"
        };
    }

    public async Task<BasicResponse> DeleteUserAsync(Guid userId, string currentAdminEmail, CancellationToken cancellationToken = default)
    {
        ValidateGuid(userId, nameof(userId));

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            throw new NotFoundException("User", userId);
        }

        if (user.Email?.Equals(currentAdminEmail, StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning("Admin {Email} attempted to delete own account", currentAdminEmail);
            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.CannotModifyOwnAccount
            };
        }

        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            _logger.LogError("Failed to delete user {Email}: {Errors}",
                user.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return new BasicResponse
            {
                Succeeded = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        _logger.LogInformation("User {Email} deleted by admin", user.Email);

        return new BasicResponse
        {
            Succeeded = true,
            Message = $"User {user.Email} has been deleted successfully."
        };
    }

    private void ValidateGuid(Guid userId, string paramName)
    {
        if (userId == Guid.Empty) throw new ValidationException($"{paramName} cannot be empty");
    }
}