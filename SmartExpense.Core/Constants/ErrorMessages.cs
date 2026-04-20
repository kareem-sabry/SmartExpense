namespace SmartExpense.Core.Constants;

public static class ErrorMessages
{
    // User errors
    public const string UserNotFound = "User not found.";
    public const string UserAlreadyExists = "A user with this email already exists.";
    public const string InvalidCredentials = "Invalid email or password.";
    public const string InvalidUserContext = "Invalid user context.";
    public const string CannotModifyOwnAccount = "You cannot modify your own account.";
    public const string RegistrationFailed = "Registration failed.";
    public const string InvalidPasswordResetRequest = "Invalid password reset request.";


    // Role errors
    public const string InvalidRole = "Invalid role specified.";
    public const string RoleDoesNotExist = "The specified role does not exist.";
    public const string UserAlreadyAdmin = "User is already an admin.";
    public const string UserNotAdmin = "User is not an admin.";
    public const string CannotRegisterAsAdmin = "You can only register as a normal user.";

    // Token errors
    public const string RefreshTokenMissing = "Refresh token is missing.";
    public const string RefreshTokenExpired = "Refresh token has expired. Please log in again.";
    public const string RefreshTokenInvalid = "Invalid refresh token.";

    public const string
        RefreshTokenReuseDetected = "Invalid refresh token."; // deliberately identical — reveals nothing to the caller but appears in structured logging

    // General errors
    public const string InvalidId = "Invalid ID provided.";
    public const string OperationFailed = "The operation failed. Please try again.";
    public const string InternalServerError = "An unexpected error occurred. Please try again later.";
}