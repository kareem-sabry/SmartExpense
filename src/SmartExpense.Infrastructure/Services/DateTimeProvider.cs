using SmartExpense.Application.Interfaces;

namespace SmartExpense.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}