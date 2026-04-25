namespace SmartExpense.Application.Dtos.Category;

public class CategoryReadDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsSystemCategory { get; set; }
    public bool IsActive { get; set; }
}