namespace SmartExpense.Core.Interfaces;

public interface IUserOwnedEntity
{
    Guid UserId { get; set; }
}