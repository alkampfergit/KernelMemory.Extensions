using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Interfaces
{
    /// <summary>
    /// An interface that allows to store multiple elements with a single call, useful
    /// for all databases that support bulk insert.
    /// </summary>
    public interface IBulkMemoryDb
    {
        /// <summary>
        /// Upsert a series of records in memory db.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="records"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IReadOnlyCollection<string>> UpsertManyAsync(
            string index,
            IEnumerable<MemoryRecord> records,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete all previously <see cref="MemoryRecord"/> of <paramref name="documentId"/> and 
        /// massively insert the new list of <see cref="MemoryRecord"/>.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="documentId">The id of the document to replace. All previous MemoryRecords
        /// for the document will be deleted and replaced with the new list of MemoryRecord</param>
        /// <param name="records"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IReadOnlyCollection<string>> ReplaceDocumentAsync(
            string index,
            string documentId,
            IEnumerable<MemoryRecord> records,
            CancellationToken cancellationToken = default);
    }
}
