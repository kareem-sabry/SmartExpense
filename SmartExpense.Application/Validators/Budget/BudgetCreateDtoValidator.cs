using FluentValidation;
using SmartExpense.Application.Dtos.Budget;

namespace SmartExpense.Application.Validators.Budget;

public class BudgetCreateDtoValidator : AbstractValidator<BudgetCreateDto>
{
    public BudgetCreateDtoValidator()
    {
        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("A valid category must be selected.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Budget amount must be greater than zero.")
            .ScalePrecision(2, 18).WithMessage("Amount cannot have more than 2 decimal places.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");
    }
}