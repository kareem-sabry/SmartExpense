using FluentValidation;
using SmartExpense.Application.Dtos.RecurringTransaction;

namespace SmartExpense.Application.Validators.RecurringTransaction;

public class RecurringTransactionUpdateDtoValidator : AbstractValidator<RecurringTransactionUpdateDto>
{
    public RecurringTransactionUpdateDtoValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("A valid category must be selected.");
        
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200).WithMessage("Description cannot be empty.");
        
        RuleFor(x => x.Amount).GreaterThan(0).ScalePrecision(2, 18).WithMessage("amound should be greater than 0");
        
        RuleFor(x => x.TransactionType).IsInEnum().WithMessage("Transaction type should be in the allowed types");
        RuleFor(x => x.Frequency).IsInEnum().WithMessage("frequency should be in the allowed options");
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null).WithMessage("notes length cannot exceed 500 characters");
    }
}