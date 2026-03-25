using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Logging;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using LoginRequest = SmartExpense.Application.Dtos.Auth.LoginRequest;
using RegisterRequest = SmartExpense.Application.Dtos.Auth.RegisterRequest;

namespace SmartExpense.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly IAuthTokenProcessor _authTokenProcessor;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountService> _logger;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<User> _userManager;
    private readonly IUserRepository _userRepository;

    public AccountService(IAuthTokenProcessor authTokenProcessor, UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager, IUserRepository userRepository,
        ILogger<AccountService> logger, IDateTimeProvider dateTimeProvider, IEmailService emailService)
    {
        _authTokenProcessor = authTokenProcessor;
        _userManager = userManager;
        _roleManager = roleManager;
        _userRepository = userRepository;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _emailService = emailService;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest registerRequest)
    {
        var userExists = await _userManager.FindByEmailAsync(registerRequest.Email) != null;

        if (userExists)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", registerRequest.Email);

            return new RegisterResponse
            {
                Succeeded = false,
                Message = ErrorMessages.UserAlreadyExists,
                Errors = new[] { ErrorMessages.UserAlreadyExists }
            };
        }

        if (registerRequest.Role != Role.User)
        {
            _logger.LogWarning("Attempt to register with invalid role: {Role}", registerRequest.Role);
            return new RegisterResponse
            {
                Succeeded = false,
                Message = ErrorMessages.InvalidRole,
                Errors = new[] { ErrorMessages.CannotRegisterAsAdmin }
            };
        }

        var identityRoleName = GetIdentityRoleName(registerRequest.Role);

        var roleExists = await _roleManager.RoleExistsAsync(identityRoleName);

        if (!roleExists)
        {
            _logger.LogError("Invalid role specified during registration: {Role}", registerRequest.Role);

            return new RegisterResponse
            {
                Succeeded = false,
                Message = ErrorMessages.InvalidRole,
                Errors = new[] { ErrorMessages.RoleDoesNotExist }
            };
        }


        var user = User.Create(registerRequest.Email, registerRequest.FirstName, registerRequest.LastName,
            _dateTimeProvider.UtcNow);

        var result = await _userManager.CreateAsync(user, registerRequest.Password);

        if (!result.Succeeded)
        {
            _logger.LogWarning("User registration failed for {Email}: {Errors}",
                registerRequest.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return new RegisterResponse
            {
                Succeeded = false,
                Message = ErrorMessages.RegistrationFailed,
                Errors = result.Errors.Select(x => x.Description)
            };
        }

        await _userManager.AddToRoleAsync(user, identityRoleName);

        _logger.LogInformation("User registered successfully: {Email} with role {Role}",
            user.Email, identityRoleName);

        return new RegisterResponse
        {
            Succeeded = true,
            Message = SuccessMessages.RegistrationSuccessful
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest loginRequest)
    {
        var user = await _userManager.FindByEmailAsync(loginRequest.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, loginRequest.Password))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", loginRequest.Email);

            return new LoginResponse
            {
                Succeeded = false,
                Message = ErrorMessages.InvalidCredentials
            };
        }


        var roles = await _userManager.GetRolesAsync(user);
        var (jwtToken, expirationDateInUtc) = _authTokenProcessor.GenerateJwtToken(user, roles);

        var refreshTokenValue = _authTokenProcessor.GenerateRefreshToken();

        var refreshTokenExpirationDateInUtc =
            _dateTimeProvider.UtcNow.AddDays(ApplicationConstants.RefreshTokenExpirationDays);

        user.RefreshToken = refreshTokenValue;
        user.RefreshTokenExpiresAtUtc = refreshTokenExpirationDateInUtc;

        await _userManager.UpdateAsync(user);
        _logger.LogInformation("User logged in successfully: {Email}", user.Email);

        return new LoginResponse
        {
            Succeeded = true,
            Message = SuccessMessages.LoginSuccessful,
            AccessToken = jwtToken,
            ExpiresAtUtc = expirationDateInUtc,
            RefreshToken = refreshTokenValue
        };
    }

    public async Task<BasicResponse> DeleteMyAccountAsync(string userEmail)
    {
        var user = await _userManager.FindByEmailAsync(userEmail);

        if (user == null)
        {
            _logger.LogWarning("Account deletion attempted for non-existent user: {Email}", userEmail);

            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.UserNotFound
            };
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to delete account for {Email}: {Errors}",
                userEmail,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.OperationFailed
            };
        }

        _logger.LogInformation("Account deleted successfully: {Email}", userEmail);

        return new BasicResponse
        {
            Succeeded = true,
            Message = SuccessMessages.AccountDeleted
        };
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest refreshTokenRequest)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenRequest.RefreshToken))
            return new RefreshTokenResponse
            {
                Succeeded = false,
                Message = ErrorMessages.RefreshTokenMissing
            };

        var user = await _userRepository.GetUserByRefreshTokenAsync(refreshTokenRequest.RefreshToken);

        if (user == null)
        {
            _logger.LogWarning("Invalid refresh token attempt");

            return new RefreshTokenResponse
            {
                Succeeded = false,
                Message = ErrorMessages.RefreshTokenInvalid
            };
        }

        if (user.RefreshTokenExpiresAtUtc < _dateTimeProvider.UtcNow)
        {
            _logger.LogInformation("Expired refresh token used for user: {Email}", user.Email);

            return new RefreshTokenResponse
            {
                Succeeded = false,
                Message = ErrorMessages.RefreshTokenExpired
            };
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (jwtToken, expirationDateInUtc) = _authTokenProcessor.GenerateJwtToken(user, roles);
        var refreshTokenValue = _authTokenProcessor.GenerateRefreshToken();
        var refreshExpirationDateInUtc =
            _dateTimeProvider.UtcNow.AddDays(ApplicationConstants.RefreshTokenExpirationDays);

        user.RefreshToken = refreshTokenValue;
        user.RefreshTokenExpiresAtUtc = refreshExpirationDateInUtc;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Token refreshed for user: {Email}", user.Email);

        return new RefreshTokenResponse
        {
            Succeeded = true,
            Message = SuccessMessages.TokenRefreshed,
            AccessToken = jwtToken,
            ExpiresAtUtc = expirationDateInUtc,
            RefreshToken = refreshTokenValue
        };
    }

    public async Task<BasicResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);

            return new BasicResponse
            {
                Succeeded = true,
                Message = SuccessMessages.PasswordResetEmailSent
            };
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        var emailBody = $"""
                         Hello {user.FirstName},

                         You requested to reset your password for SmartExpense.Api.

                         Please use the following token to reset your password:

                         {token}

                         This token will expire in {ApplicationConstants.PasswordResetTokenExpirationHours} hours.

                         If you didn't request this password reset, please ignore this email and your password will remain unchanged.

                         For security reasons, never share this token with anyone.

                         Best regards,
                         SmartExpense.Api Support Team
                         """;

        await _emailService.SendEmailAsync(
            user.Email!,
            "SmartExpense.Api Password Reset",
            emailBody
        );
        _logger.LogInformation("Password reset email sent to: {Email}", user.Email);

        return new BasicResponse
        {
            Succeeded = true,
            Message = SuccessMessages.PasswordResetEmailSent
        };
    }

    public async Task<BasicResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            _logger.LogWarning("Password reset attempted for non-existent email: {Email}", request.Email);

            return new BasicResponse
            {
                Succeeded = false,
                Message = ErrorMessages.InvalidPasswordResetRequest
            };
        }

        var result = await _userManager.ResetPasswordAsync(user, request.ResetCode, request.NewPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Password reset failed for {Email}: {Errors}",
                request.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return new BasicResponse
            {
                Succeeded = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiresAtUtc = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password reset successfully for: {Email}", user.Email);

        return new BasicResponse
        {
            Succeeded = true,
            Message = SuccessMessages.PasswordResetSuccessful
        };
    }

    public async Task<LogoutResponse> LogoutAsync(string userEmail)
    {
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            _logger.LogWarning("Logout attempted for non-existent user: {Email}", userEmail);

            return new LogoutResponse
            {
                Succeeded = false,
                Message = ErrorMessages.UserNotFound
            };
        }

        // Invalidate the refresh token
        user.RefreshToken = null;
        user.RefreshTokenExpiresAtUtc = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User logged out: {Email}", userEmail);

        return new LogoutResponse
        {
            Succeeded = true,
            Message = SuccessMessages.LogoutSuccessful
        };
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(string userEmail)
    {
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            _logger.LogWarning("User profile requested for non-existent user: {Email}", userEmail);

            return null;
        }

        return new UserProfileDto
        {
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAtUtc = user.CreatedAtUtc,
            UpdatedAtUtc = user.UpdatedAtUtc
        };
    }

    private string GetIdentityRoleName(Role role)
    {
        return role switch
        {
            Role.User => IdentityRoleConstants.User,
            Role.Admin => IdentityRoleConstants.Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Provided role is not supported.")
        };
    }
}