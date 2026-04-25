namespace SmartExpense.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    ICategoryRepository Categories { get; }
    ITransactionRepository Transactions { get; }
    IBudgetRepository Budgets { get; }
    IRecurringTransactionRepository RecurringTransactions { get; }
    IUserRepository Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}