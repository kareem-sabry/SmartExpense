namespace SmartExpense.Core.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entity, object key) : base($"{entity} with identifier '{key}' was not found.")
    {
    }
}