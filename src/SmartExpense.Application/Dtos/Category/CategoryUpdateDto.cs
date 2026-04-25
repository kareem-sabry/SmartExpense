namespace SmartExpense.Application.Dtos.Category;

public class CategoryUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
}