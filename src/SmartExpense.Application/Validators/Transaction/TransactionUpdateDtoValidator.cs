using FluentValidation;
using SmartExpense.Application.Dtos.Transaction;

namespace SmartExpense.Application.Validators.Transaction;

public class TransactionUpdateDtoValidator : AbstractValidator<TransactionUpdateDto>
{
    public TransactionUpdateDtoValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("A valid category must be selected.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(200).WithMessage("Description cannot exceed 200 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .ScalePrecision(2, 18).WithMessage("Amount cannot have more than 2 decimal places.");

        RuleFor(x => x.TransactionType)
            .IsInEnum().WithMessage("Transaction type must be Income or Expense.");

        RuleFor(x => x.TransactionDate)
            .NotEmpty().WithMessage("Transaction date is required.")
            .LessThanOrEqualTo(_ => DateTime.UtcNow).WithMessage("Transaction date cannot be in the future.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes is not null);
    }
}