using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Infrastructure.Services;
using ForgotPasswordRequest = SmartExpense.Application.Dtos.Auth.ForgotPasswordRequest;
using LoginRequest = SmartExpense.Application.Dtos.Auth.LoginRequest;
using RegisterRequest = SmartExpense.Application.Dtos.Auth.RegisterRequest;
using ResetPasswordRequest = SmartExpense.Application.Dtos.Auth.ResetPasswordRequest;
namespace SmartExpense.Tests.Services;

public class AccountServiceTests
{
    // ── Infrastructure mocks ──────────────────────────────────────────────────
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<RoleManager<IdentityRole<Guid>>> _roleManagerMock;
    private readonly Mock<IAuthTokenProcessor> _authTokenProcessorMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IEmailService> _emailServiceMock;

    private readonly AccountService _sut;

    private readonly DateTime _fixedNow = new(2025, 4, 20, 10, 0, 0, DateTimeKind.Utc);
    private const string PlainRefreshToken = "plain-refresh-token-64-bytes-base64-output-here";

    public AccountServiceTests()
    {
        // UserManager requires an IUserStore mock; remaining constructor args can be null.
        var userStore = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        _roleManagerMock = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStore.Object, null!, null!, null!, null!);

        _authTokenProcessorMock = new Mock<IAuthTokenProcessor>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _emailServiceMock = new Mock<IEmailService>();

        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_fixedNow);
        _unitOfWorkMock.Setup(x => x.Users).Returns(_userRepositoryMock.Object);

        _sut = new AccountService(
            _authTokenProcessorMock.Object,
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _unitOfWorkMock.Object,
            Mock.Of<ILogger<AccountService>>(),
            _dateTimeProviderMock.Object,
            _emailServiceMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private User BuildUser(string email = "user@test.com") => User.Create(email, "Jane", "Smith", _fixedNow);

    // ═════════════════════════════════════════════════════════════════════════
    // RegisterAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterAsync_WithValidRequest_CreatesUserCorrectly()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "new@test.com",
            FirstName = "Jane",
            LastName = "Smith",
            Password = "Strong@123",
            Role = Role.User
        };

        User? createdUser = null;

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _roleManagerMock.Setup(x => x.RoleExistsAsync(IdentityRoleConstants.User))
            .ReturnsAsync(true);

        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), request.Password))
            .Callback<User, string>((u, _) => createdUser = u)
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), IdentityRoleConstants.User))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();

        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be(request.Email);
        createdUser.FirstName.Should().Be(request.FirstName);
        createdUser.LastName.Should().Be(request.LastName);
        createdUser.CreatedAtUtc.Should().Be(_fixedNow);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "exists@test.com",
            FirstName = "Jane",
            LastName = "Smith",
            Password = "Strong@123",
            Role = Role.User
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(BuildUser(request.Email));

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.UserAlreadyExists);
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithAdminRole_ReturnsFailure_AndNeverCreatesUser()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "hacker@test.com",
            FirstName = "Bad",
            LastName = "Actor",
            Password = "Strong@123",
            Role = Role.Admin // must be blocked
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.InvalidRole);
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenIdentityCreateFails_ReturnsFailureWithErrors()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "new@test.com", FirstName = "Jane", LastName = "Smith",
            Password = "weak", Role = Role.User
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);
        _roleManagerMock.Setup(x => x.RoleExistsAsync(IdentityRoleConstants.User))
            .ReturnsAsync(true);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Password too weak.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LoginAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokensAndStoresHash()
    {
        // Arrange
        var user = BuildUser();
        var request = new LoginRequest { Email = user.Email!, Password = "Correct@123" };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, request.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new[] { IdentityRoleConstants.User });
        _authTokenProcessorMock
            .Setup(x => x.GenerateJwtToken(user, It.IsAny<IList<string>>()))
            .Returns(("jwt.token.here", _fixedNow.AddMinutes(15)));
        _authTokenProcessorMock
            .Setup(x => x.GenerateRefreshToken())
            .Returns(PlainRefreshToken);
        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert — client receives the plain token
        result.Succeeded.Should().BeTrue();
        result.RefreshToken.Should().Be(PlainRefreshToken);
        result.AccessToken.Should().Be("jwt.token.here");

        // Assert — stored value is the hash, not the plain token
        _userManagerMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
            u.RefreshToken == HashToken(PlainRefreshToken) &&
            u.RefreshToken != PlainRefreshToken
        )), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsFailure()
    {
        // Arrange
        var user = BuildUser();
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);

        // Act
        var result = await _sut.LoginAsync(new LoginRequest { Email = user.Email!, Password = "Wrong" });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.InvalidCredentials);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentEmail_ReturnsFailure()
    {
        // Arrange
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = "Any" });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.InvalidCredentials);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RefreshTokenAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_RotatesHashAndReturnsNewTokens()
    {
        // Arrange
        var oldHash = HashToken(PlainRefreshToken);
        var user = BuildUser();
        user.RefreshToken = oldHash;
        user.RefreshTokenExpiresAtUtc = _fixedNow.AddDays(7);

        const string newPlainToken = "brand-new-64-byte-refresh-token-base64";

        _userRepositoryMock
            .Setup(x => x.GetUserByRefreshTokenAsync(oldHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new[] { IdentityRoleConstants.User });
        _authTokenProcessorMock
            .Setup(x => x.GenerateJwtToken(user, It.IsAny<IList<string>>()))
            .Returns(("new.jwt", _fixedNow.AddMinutes(15)));
        _authTokenProcessorMock.Setup(x => x.GenerateRefreshToken()).Returns(newPlainToken);
        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = PlainRefreshToken });

        // Assert — client receives new plain token
        result.Succeeded.Should().BeTrue();
        result.RefreshToken.Should().Be(newPlainToken);

        // Assert — old hash slides into Previous, new hash stored as current
        _userManagerMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
            u.PreviousRefreshTokenHash == oldHash &&
            u.RefreshToken == HashToken(newPlainToken)
        )), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ReturnsFailure()
    {
        // Arrange
        var user = BuildUser();
        user.RefreshToken = HashToken(PlainRefreshToken);
        user.RefreshTokenExpiresAtUtc = _fixedNow.AddDays(-1); // expired yesterday

        _userRepositoryMock
            .Setup(x => x.GetUserByRefreshTokenAsync(HashToken(PlainRefreshToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = PlainRefreshToken });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.RefreshTokenExpired);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithCompletelyUnknownToken_ReturnsInvalid()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.GetUserByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepositoryMock
            .Setup(x => x.GetUserByPreviousRefreshTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "garbage-token" });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.RefreshTokenInvalid);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenReuseOfRotatedTokenDetected_RevokesAllTokensAndReturnsInvalid()
    {
        // Arrange — incoming token matches PreviousRefreshTokenHash → theft signal
        const string rotatedAwayToken = "old-token-already-rotated-64-bytes";
        var compromisedUser = BuildUser();
        compromisedUser.RefreshToken = HashToken("current-valid-token-64-bytes");
        compromisedUser.PreviousRefreshTokenHash = HashToken(rotatedAwayToken);
        compromisedUser.RefreshTokenExpiresAtUtc = _fixedNow.AddDays(7);

        _userRepositoryMock
            .Setup(x => x.GetUserByRefreshTokenAsync(HashToken(rotatedAwayToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null); // not current token

        _userRepositoryMock
            .Setup(x => x.GetUserByPreviousRefreshTokenHashAsync(HashToken(rotatedAwayToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(compromisedUser); // matches previous hash

        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = rotatedAwayToken });

        // Assert — all tokens wiped
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.RefreshTokenInvalid);

        _userManagerMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
            u.RefreshToken == null &&
            u.PreviousRefreshTokenHash == null &&
            u.RefreshTokenExpiresAtUtc == null
        )), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithMissingToken_ReturnsFailure_WithoutHittingRepository()
    {
        // Act
        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "   " });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.RefreshTokenMissing);
        _userRepositoryMock.Verify(
            x => x.GetUserByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LogoutAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LogoutAsync_WithValidUser_NullsRefreshTokenAndSucceeds()
    {
        // Arrange
        var user = BuildUser();
        user.RefreshToken = HashToken(PlainRefreshToken);
        user.RefreshTokenExpiresAtUtc = _fixedNow.AddDays(7);

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.LogoutAsync(user.Email!);

        // Assert
        result.Succeeded.Should().BeTrue();
        _userManagerMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
            u.RefreshToken == null &&
            u.RefreshTokenExpiresAtUtc == null
        )), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.LogoutAsync("nobody@test.com");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.UserNotFound);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ForgotPasswordAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ForgotPasswordAsync_WithExistingEmail_SendsEmailAndReturns200()
    {
        // Arrange
        var user = BuildUser();
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("raw+token/==");

        // Act
        var result = await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = user.Email! });

        // Assert — always 200 regardless
        result.Succeeded.Should().BeTrue();
        result.Message.Should().Be(SuccessMessages.PasswordResetEmailSent);

        // Assert — email was actually sent
        _emailServiceMock.Verify(x => x.SendEmailAsync(
            user.Email!,
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("reset-password") && !body.Contains("raw+token/==")) // URL-encoded
        ), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithNonExistentEmail_Returns200WithoutSendingEmail()
    {
        // Arrange
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = "ghost@test.com" });

        // Assert — same 200 response to prevent user enumeration
        result.Succeeded.Should().BeTrue();
        result.Message.Should().Be(SuccessMessages.PasswordResetEmailSent);
        _emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ResetPasswordAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_SucceedsAndRevokesRefreshToken()
    {
        // Arrange
        var user = BuildUser();
        user.RefreshToken = HashToken(PlainRefreshToken);
        user.RefreshTokenExpiresAtUtc = _fixedNow.AddDays(7);

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.ResetPasswordAsync(user, It.IsAny<string>(), "NewPass@123"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = user.Email!, ResetCode = "valid-encoded-token", NewPassword = "NewPass@123"
        });

        // Assert
        result.Succeeded.Should().BeTrue();

        // Refresh tokens must be wiped after password reset
        _userManagerMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
            u.RefreshToken == null &&
            u.RefreshTokenExpiresAtUtc == null
        )), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithNonExistentEmail_ReturnsFailure()
    {
        // Arrange
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "nobody@test.com", ResetCode = "token", NewPassword = "NewPass@123"
        });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.InvalidPasswordResetRequest);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInvalidToken_ReturnsFailureFromIdentity()
    {
        // Arrange
        var user = BuildUser();
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _userManagerMock
            .Setup(x => x.ResetPasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));

        // Act
        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = user.Email!, ResetCode = "bad-token", NewPassword = "NewPass@123"
        });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Invalid token.");
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DeleteMyAccountAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteMyAccountAsync_WithValidEmail_DeletesUserAndSucceeds()
    {
        // Arrange
        var user = BuildUser();
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.DeleteMyAccountAsync(user.Email!);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Message.Should().Be(SuccessMessages.AccountDeleted);
        _userManagerMock.Verify(x => x.DeleteAsync(user), Times.Once);
    }

    [Fact]
    public async Task DeleteMyAccountAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.DeleteMyAccountAsync("ghost@test.com");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be(ErrorMessages.UserNotFound);
        _userManagerMock.Verify(x => x.DeleteAsync(It.IsAny<User>()), Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetCurrentUserAsync
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCurrentUserAsync_WithValidEmail_ReturnsProfile()
    {
        // Arrange
        var user = BuildUser("profile@test.com");
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email!)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetCurrentUserAsync(user.Email!);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(user.Email);
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithNonExistentEmail_ReturnsNull()
    {
        // Arrange
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.GetCurrentUserAsync("ghost@test.com");

        // Assert
        result.Should().BeNull();
    }
}