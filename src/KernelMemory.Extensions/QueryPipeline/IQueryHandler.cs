using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

/// <summary>
/// Handlers are responsible for handling user questions to perform various operations
/// </summary>
public interface IQueryHandler
{
    /// <summary>
    /// Handle the question, usually it will modify the UserQuestion object until we reach
    /// the final step where we have all segments used to generate the answer.
    /// </summary>
    /// <param name="userQuestion"></param>
    /// <returns></returns>
    Task HandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken);

    /// <summary>
    /// Each handler has a name to identify it.
    /// </summary>
    string Name { get; }
}
