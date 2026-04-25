using FluentValidation;
using SmartExpense.Application.Dtos.Category;

namespace SmartExpense.Application.Validators.Category;

public class CategoryCreateDtoValidator : AbstractValidator<CategoryCreateDto>
{
    // Hex color regex: #RGB or #RRGGBB
    private static readonly System.Text.RegularExpressions.Regex HexColorRegex =
        new(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public CategoryCreateDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters.");

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("Icon identifier cannot exceed 50 characters.")
            .When(x => x.Icon is not null);

        RuleFor(x => x.Color)
            .Must(c => c == null || HexColorRegex.IsMatch(c))
            .WithMessage("Color must be a valid hex code (e.g. #FF5733 or #F53).")
            .When(x => x.Color is not null);
    }
}