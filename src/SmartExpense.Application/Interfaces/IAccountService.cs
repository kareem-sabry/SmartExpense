using SmartExpense.Application.Dtos.Auth;
using ForgotPasswordRequest = SmartExpense.Application.Dtos.Auth.ForgotPasswordRequest;
using LoginRequest = SmartExpense.Application.Dtos.Auth.LoginRequest;
using RegisterRequest = SmartExpense.Application.Dtos.Auth.RegisterRequest;
using ResetPasswordRequest = SmartExpense.Application.Dtos.Auth.ResetPasswordRequest;

namespace SmartExpense.Application.Interfaces;

public interface IAccountService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken = default);
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest refreshTokenRequest, CancellationToken cancellationToken = default);
    Task<LogoutResponse> LogoutAsync(string userEmail, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> GetCurrentUserAsync(string userEmail, CancellationToken cancellationToken = default);
    Task<BasicResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<BasicResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<BasicResponse> DeleteMyAccountAsync(string userEmail, CancellationToken cancellationToken = default);
}