using FluentValidation;
using SmartExpense.Application.Dtos.Auth;
using SmartExpense.Core.Constants;

namespace SmartExpense.Application.Validators.Auth;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.ResetCode)
            .NotEmpty().WithMessage("Reset code is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(ApplicationConstants.MinPasswordLength)
            .WithMessage($"Password must be at least {ApplicationConstants.MinPasswordLength} characters.")
            .MaximumLength(ApplicationConstants.MaxPasswordLength)
            .WithMessage($"Password cannot exceed {ApplicationConstants.MaxPasswordLength} characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}