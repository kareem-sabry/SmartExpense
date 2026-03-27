using Microsoft.AspNetCore.Identity.Data;
using SmartExpense.Application.Dtos.Auth;
using LoginRequest = SmartExpense.Application.Dtos.Auth.LoginRequest;
using RegisterRequest = SmartExpense.Application.Dtos.Auth.RegisterRequest;

namespace SmartExpense.Application.Interfaces;

public interface IAccountService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest registerRequest);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest refreshTokenRequest);
    Task<LogoutResponse> LogoutAsync(string userEmail);
    Task<UserProfileDto?> GetCurrentUserAsync(string userEmail);
    Task<BasicResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<BasicResponse> ResetPasswordAsync(ResetPasswordRequest request);
    Task<BasicResponse> DeleteMyAccountAsync(string userEmail);
}