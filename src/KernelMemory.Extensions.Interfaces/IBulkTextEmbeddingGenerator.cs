using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Interfaces
{
    /// <summary>
    /// For better performances we can support embedding generators that
    /// can process multiple texts at once.
    /// </summary>
    public interface IBulkTextEmbeddingGenerator : ITextEmbeddingGenerator
    {
        Task<Embedding[]> GenerateEmbeddingsAsync(
            string[] text,
            CancellationToken cancellationToken = default);
    }
}
