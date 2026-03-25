using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;

namespace SmartExpense.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Roles = IdentityRoleConstants.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    ///     Returns a list of all registered users with their assigned roles.
    ///     Accessible by administrators only.
    /// </summary>
    /// <returns>A collection of users including their profile details and roles.</returns>
    /// <response code="200">Users retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<UserWithRolesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken = default)
    {
        var users = await _adminService.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    ///     Returns a single user by their ID, including their assigned roles.
    ///     Accessible by administrators only.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to retrieve.</param>
    /// <returns>The user's profile and role information.</returns>
    /// <response code="200">User found and returned.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    /// <response code="404">No user with the given ID was found.</response>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType(typeof(UserWithRolesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _adminService.GetUserByIdAsync(userId);
        return Ok(user);
    }

    /// <summary>
    ///     Grants the Admin role to the specified user.
    ///     The requesting administrator cannot promote themselves.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to promote.</param>
    /// <returns>A response indicating whether the role was assigned successfully.</returns>
    /// <response code="200">Admin role granted successfully.</response>
    /// <response code="400">The operation failed (e.g. user already has the Admin role).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    /// <response code="404">No user with the given ID was found.</response>
    [HttpPost("users/{userId:guid}/make-admin")]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MakeUserAdmin(Guid userId, CancellationToken cancellationToken = default)
    {
        var currentAdminEmail = User.FindFirstValue(ClaimTypes.Email)!;
        var response = await _adminService.MakeUserAdminAsync(userId, currentAdminEmail);
        return Ok(response);
    }

    /// <summary>
    ///     Revokes the Admin role from the specified user.
    ///     An administrator cannot remove their own Admin role.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose Admin role should be removed.</param>
    /// <returns>A response indicating whether the role was removed successfully.</returns>
    /// <response code="200">Admin role removed successfully.</response>
    /// <response code="400">The operation failed (e.g. attempting to demote yourself).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    /// <response code="404">No user with the given ID was found.</response>
    [HttpPost("users/{userId:guid}/remove-admin")]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAdminRole(Guid userId, CancellationToken cancellationToken = default)
    {
        var currentAdminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var response = await _adminService.RemoveAdminRoleAsync(userId, currentAdminId);
        return Ok(response);
    }

    /// <summary>
    ///     Permanently deletes a user account by ID.
    ///     Administrators cannot delete their own account through this endpoint.
    ///     This operation is irreversible.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to delete.</param>
    /// <returns>A response indicating whether the account was deleted successfully.</returns>
    /// <response code="200">User account deleted successfully.</response>
    /// <response code="400">The operation failed (e.g. attempting to delete yourself).</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    /// <response code="404">No user with the given ID was found.</response>
    [HttpDelete("users/{userId:guid}")]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default)
    {
        var currentAdminEmail = User.FindFirstValue(ClaimTypes.Email)!;
        var response = await _adminService.DeleteUserAsync(userId, currentAdminEmail);
        return Ok(response);
    }
}