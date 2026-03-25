using Microsoft.EntityFrameworkCore.Storage;
using SmartExpense.Application.Interfaces;
using SmartExpense.Infrastructure.Repositories;

namespace SmartExpense.Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IBudgetRepository? _budgets;
    private ICategoryRepository? _categories;
    private IRecurringTransactionRepository? _recurringTransactions;
    private IDbContextTransaction? _transaction;
    private ITransactionRepository? _transactions;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public ICategoryRepository Categories =>
        _categories ??= new CategoryRepository(_context);

    public ITransactionRepository Transactions =>
        _transactions ??= new TransactionRepository(_context);

    public IBudgetRepository Budgets =>
        _budgets ??= new BudgetRepository(_context);

    public IRecurringTransactionRepository RecurringTransactions =>
        _recurringTransactions ??= new RecurringTransactionRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}