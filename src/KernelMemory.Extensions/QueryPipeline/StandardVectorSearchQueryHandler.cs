using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions
{
    public class StandardVectorSearchQueryHandler : BasicQueryHandler
    {
        private readonly IMemoryDb _memoryDb;
        private readonly ILogger<StandardVectorSearchQueryHandler> _log;

        public override string Name => "StandardVectorSearchQueryHandler";

        public StandardVectorSearchQueryHandler(
            IMemoryDb memory,
            ILogger<StandardVectorSearchQueryHandler>? log = null)
        {
            _memoryDb = memory;
            _log = log ?? DefaultLogger<StandardVectorSearchQueryHandler>.Instance;
        }

        /// <summary>
        /// Perform a vector search in default memory
        /// </summary>
        /// <param name="userQuestion"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task OnHandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
        {
            var list = new List<(MemoryRecord memory, double relevance)>();

            IAsyncEnumerable<(MemoryRecord, double)> matches = this._memoryDb.GetSimilarListAsync(
                index: userQuestion.UserQueryOptions.Index,
                text: userQuestion.Question,
                filters: userQuestion.Filters,
                minRelevance: userQuestion.UserQueryOptions.MinRelevance,
                limit: userQuestion.UserQueryOptions.RetrievalQueryLimit,
                withEmbeddings: false,
                cancellationToken: cancellationToken);

            // Memories are sorted by relevance, starting from the most relevant
            await foreach ((MemoryRecord memory, double relevance) in matches.ConfigureAwait(false))
            {
                list.Add((memory, relevance));
            }

            var records = new List<MemoryRecord>();
            // Memories are sorted by relevance, starting from the most relevant
            foreach ((MemoryRecord memory, double relevance) in list)
            {
                var partitionText = memory.GetPartitionText(this._log).Trim();
                if (string.IsNullOrEmpty(partitionText))
                {
                    this._log.LogError("The document partition is empty, doc: {0}", memory.Id);
                    continue;
                }

                if (relevance > float.MinValue)
                {
                    this._log.LogTrace("Adding result with relevance {0}", relevance);
                    records.Add(memory);
                }
            }

            //ok now that you have all the memory record and citations, add to the object
            userQuestion.AddMemoryRecordSource("standard-vector-search", records);
        }
    }
}
