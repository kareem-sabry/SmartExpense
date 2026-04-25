namespace SmartExpense.Core.Constants;

public class SuccessMessages
{
    // User messages
    public const string RegistrationSuccessful = "User registered successfully.";
    public const string LoginSuccessful = "Login successful.";
    public const string LogoutSuccessful = "User logged out successfully.";
    public const string AccountDeleted = "Account deleted successfully.";

    // Password messages
    public const string PasswordResetEmailSent =
        "If an account exists with that email, a password reset link has been sent.";

    public const string PasswordResetSuccessful = "Password has been reset successfully.";

    // Token messages
    public const string TokenRefreshed = "Token refreshed successfully.";

    // Role messages
    public const string AdminRoleGranted = "Admin role has been granted.";
    public const string AdminRoleRemoved = "Admin role has been removed.";
}