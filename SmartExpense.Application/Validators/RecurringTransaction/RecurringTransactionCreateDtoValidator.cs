using FluentValidation;
using SmartExpense.Application.Dtos.RecurringTransaction;

namespace SmartExpense.Application.Validators.RecurringTransaction;

public class RecurringTransactionCreateDtoValidator : AbstractValidator<RecurringTransactionCreateDto>
{
    public RecurringTransactionCreateDtoValidator()
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

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Frequency must be Daily, Weekly, Monthly, or Yearly.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        // Cross-property rule — impossible with DataAnnotations
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes is not null);
    }
}