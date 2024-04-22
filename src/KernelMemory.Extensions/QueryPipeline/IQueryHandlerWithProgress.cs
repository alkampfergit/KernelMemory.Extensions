using System.Collections.Generic;
using System.Threading;

namespace KernelMemory.Extensions;

/// <summary>
/// Generic interface that handle a query, it can expand query, perform vector search or keyword search
/// or it can call an LLM to answer the query.
/// Usually you add to the pipeline a series of query retrieval handlers, then a query answer. You will also
/// have the ability to use a re-ranker to perform re-ranking of the result. The pipeline will have a stupid
/// re-rank that actually does not re-rank but just concatenate the results.
/// </summary>
public interface IQueryHandlerWithProgress : IQueryHandler
{
    /// <summary>
    /// Same functions of HandleAsync but with streaming support, because it is capable of raising 
    /// events while the answer is being generated.
    /// </summary>
    /// <param name="userQuestion"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<UserQuestionProgress> HandleStreamingAsync(UserQuestion userQuestion, CancellationToken cancellationToken);
}
