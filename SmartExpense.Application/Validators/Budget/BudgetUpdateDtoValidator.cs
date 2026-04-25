using FluentValidation;
using SmartExpense.Application.Dtos.Budget;

namespace SmartExpense.Application.Validators.Budget;

public class BudgetUpdateDtoValidator : AbstractValidator<BudgetUpdateDto>
{
    public BudgetUpdateDtoValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Budget amount must be greater than zero.")
            .ScalePrecision(2, 18).WithMessage("Amount cannot have more than 2 decimal places.");
    }
}