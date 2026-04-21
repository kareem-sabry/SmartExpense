using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;
using LoginRequest = SmartExpense.Application.Dtos.Auth.LoginRequest;
using RegisterRequest = SmartExpense.Application.Dtos.Auth.RegisterRequest;

namespace SmartExpense.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AuthController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    ///     Registers a new user account with the provided credentials.
    ///     Rate-limited to prevent abuse.
    /// </summary>
    /// <param name="registerRequest">The registration details including email and password.</param>
    /// <returns>A response indicating whether registration succeeded, including any validation errors.</returns>
    /// <response code="200">Registration succeeded.</response>
    /// <response code="400">Validation failed or the email is already in use.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registerRequest)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var response = await _accountService.RegisterAsync(registerRequest);

        if (response.Succeeded) return Ok(response);

        return BadRequest(response);
    }

    /// <summary>
    ///     Authenticates a user with their email and password and returns a JWT access token
    ///     along with a refresh token. Rate-limited to prevent brute-force attacks.
    /// </summary>
    /// <param name="loginRequest">The login credentials (email and password).</param>
    /// <returns>A response containing the JWT and refresh token on success.</returns>
    /// <response code="200">Login succeeded. JWT and refresh token returned.</response>
    /// <response code="400">Validation failed or request body is malformed.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            // FIX: was incorrectly returning RegisterResponse on a login action
            return BadRequest(new LoginResponse
            {
                Succeeded = false,
                Message = string.Join("; ", errors)
            });
        }

        var response = await _accountService.LoginAsync(loginRequest);
        if (response.Succeeded) return Ok(response);

        return Unauthorized(response);
    }

    /// <summary>
    ///     Issues a new JWT access token using a valid refresh token.
    ///     Use this endpoint to silently re-authenticate after the access token expires.
    /// </summary>
    /// <param name="refreshTokenRequest">The expired or near-expiry access token and its associated refresh token.</param>
    /// <returns>A new JWT access token and refresh token pair on success.</returns>
    /// <response code="200">Token refreshed successfully.</response>
    /// <response code="400">Validation failed or request body is malformed.</response>
    /// <response code="401">The refresh token is invalid, expired, or has been revoked.</response>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest refreshTokenRequest)
    {
        if (!ModelState.IsValid)
        {
            var errorMessage = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            return BadRequest(new LoginResponse
            {
                Succeeded = false,
                Message = errorMessage
            });
        }

        var response = await _accountService.RefreshTokenAsync(refreshTokenRequest);
        if (!response.Succeeded) return Unauthorized(response);

        return Ok(response);
    }

    /// <summary>
    ///     Logs out the currently authenticated user by invalidating their refresh token.
    ///     Requires a valid JWT.
    /// </summary>
    /// <returns>A response confirming the logout outcome.</returns>
    /// <response code="200">Logout succeeded.</response>
    /// <response code="400">The authenticated user's context is invalid or missing.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new LogoutResponse
            {
                Succeeded = false,
                Message = ErrorMessages.InvalidUserContext
            });

        var response = await _accountService.LogoutAsync(email);
        if (response.Succeeded) return Ok(response);

        return BadRequest(response);
    }

    /// <summary>
    ///     Returns the profile information of the currently authenticated user.
    /// </summary>
    /// <returns>The user's profile including email, roles, and account details.</returns>
    /// <response code="200">User profile retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    /// <response code="404">No user matching the authenticated identity was found.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.InvalidUserContext
            });

        var user = await _accountService.GetCurrentUserAsync(email);
        if (user == null)
            return NotFound(new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.UserNotFound
            });
        return Ok(user);
    }

    /// <summary>
    ///     Initiates the password reset flow by sending a reset link to the provided email address.
    ///     Always returns 200 to avoid leaking whether an account exists for the given email.
    /// </summary>
    /// <param name="request">The request containing the user's email address.</param>
    /// <returns>A response confirming the reset email was dispatched (regardless of whether the email exists).</returns>
    /// <response code="200">Password reset email sent (or silently skipped if email not found).</response>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errorMessage = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            return BadRequest(new BasicResponse
            {
                Succeeded = false,
                Message = errorMessage
            });
        }

        var response = await _accountService.ForgotPasswordAsync(request);
        return Ok(response);
    }

    /// <summary>
    ///     Resets the user's password using the token issued by the forgot-password flow.
    ///     The token is single-use and expires after a short window.
    /// </summary>
    /// <param name="request">The reset request containing the token, email, and new password.</param>
    /// <returns>A response indicating whether the password was successfully reset.</returns>
    /// <response code="200">Password reset successfully.</response>
    /// <response code="400">The token is invalid or expired, or the new password fails validation.</response>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errorMessage = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            return BadRequest(new BasicResponse
            {
                Succeeded = false,
                Message = errorMessage
            });
        }

        var response = await _accountService.ResetPasswordAsync(request);
        if (response.Succeeded) return Ok(response);

        return BadRequest(response);
    }

    /// <summary>
    ///     Permanently deletes the account of the currently authenticated user.
    ///     This operation is irreversible and removes all associated data.
    /// </summary>
    /// <returns>A response confirming whether the account was successfully deleted.</returns>
    /// <response code="200">Account deleted successfully.</response>
    /// <response code="400">The authenticated user's context is invalid or the deletion failed.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [Authorize]
    [HttpDelete("delete-account")]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMyAccount()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new LogoutResponse
            {
                Succeeded = false,
                Message = ErrorMessages.InvalidUserContext
            });

        var result = await _accountService.DeleteMyAccountAsync(email);
        if (result.Succeeded) return Ok(result);

        return BadRequest(result);
    }
}