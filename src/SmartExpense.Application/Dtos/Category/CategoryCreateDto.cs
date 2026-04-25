namespace SmartExpense.Application.Dtos.Category;

public class CategoryCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
}