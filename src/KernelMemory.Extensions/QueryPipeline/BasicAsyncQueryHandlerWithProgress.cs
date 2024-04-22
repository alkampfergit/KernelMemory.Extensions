using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

public abstract class BasicAsyncQueryHandlerWithProgress : IQueryHandlerWithProgress
{
    public abstract string Name { get; }

    public async Task HandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
    {
        //we can delegate to the async enumerable
        var enumerable = HandleStreamingAsync(userQuestion, cancellationToken);
        await foreach (var progress in enumerable)
        {
            //Actually since the client is not interested in the streaming, we can simply ignore
            //all progress messages.
        }
    }

    protected abstract IAsyncEnumerable<UserQuestionProgress> OnHandleStreamingAsync(UserQuestion userQuestion, CancellationToken cancellationToken);

    public IAsyncEnumerable<UserQuestionProgress> HandleStreamingAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
    {
        return OnHandleStreamingAsync(userQuestion, cancellationToken);
    }
}
