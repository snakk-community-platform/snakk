namespace Snakk.Application.Services;

public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}
