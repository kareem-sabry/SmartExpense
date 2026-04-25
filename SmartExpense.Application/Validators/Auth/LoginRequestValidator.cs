using FluentValidation;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Core.Constants;

namespace SmartExpense.Application.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(ApplicationConstants.MinPasswordLength)
            .WithMessage($"Password must be at least {ApplicationConstants.MinPasswordLength} characters.")
            .MaximumLength(ApplicationConstants.MaxPasswordLength)
            .WithMessage($"Password cannot exceed {ApplicationConstants.MaxPasswordLength} characters.");
    }
}