using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

/// <summary>
/// Useful when you want to wrap multiple query handlers
/// and you want them to execute concurrently
/// </summary>
public class ParallelQueryHandler : BasicQueryHandler
{
    private readonly IEnumerable<IQueryHandler> _handlers;
    private readonly string _name;

    public ParallelQueryHandler(string name, params IQueryHandler[] handlers)
    {
        _name = name;
        _handlers = handlers;
    }

    public override string Name => _name;

    protected override async Task OnHandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
    {
        var tasks = _handlers.Select(h => h.HandleAsync(userQuestion, cancellationToken));
        await Task.WhenAll(tasks);
    }
}
