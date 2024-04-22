using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

/// <summary>
/// Basic class to implement a query handler.
/// </summary>
public abstract class BasicQueryHandler : IQueryHandler
{
    public abstract string Name { get; }

    public Task HandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
    {
        return OnHandleAsync(userQuestion, cancellationToken);
    }

    protected abstract Task OnHandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken);
}
