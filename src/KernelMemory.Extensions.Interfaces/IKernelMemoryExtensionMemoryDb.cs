using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Threading;

namespace KernelMemory.Extensions.Interfaces
{
    /// <summary>
    /// You can register some MemoryDb that implements this interface to 
    /// implement some extension search, like keyword search.
    /// </summary>
    public interface IKernelMemoryExtensionMemoryDb
    {
        /// <summary>
        /// Perform a keyword search in memory db.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="query"></param>
        /// <param name="filters"></param>
        /// <param name="limit"></param>
        /// <param name="withEmbeddings"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<MemoryRecord> SearchKeywordAsync(
          string index,
          string query,
          ICollection<MemoryFilter>? filters = null,
          int limit = 1,
          bool withEmbeddings = false,
          CancellationToken cancellationToken = default);
    }
}
