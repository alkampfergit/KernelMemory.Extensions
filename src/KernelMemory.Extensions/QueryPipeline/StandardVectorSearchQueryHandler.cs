using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
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
            var citations = new List<Citation>();
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

            // Memories are sorted by relevance, starting from the most relevant
            foreach ((MemoryRecord memory, double relevance) in list)
            {
                // Note: a document can be composed by multiple files
                string documentId = memory.GetDocumentId(this._log);

                // Identify the file in case there are multiple files
                string fileId = memory.GetFileId(this._log);

                // TODO: URL to access the file in content storage
                string linkToFile = $"{userQuestion.UserQueryOptions.Index}/{documentId}/{fileId}";

                var partitionText = memory.GetPartitionText(this._log).Trim();
                if (string.IsNullOrEmpty(partitionText))
                {
                    this._log.LogError("The document partition is empty, doc: {0}", memory.Id);
                    continue;
                }

                if (relevance > float.MinValue) { this._log.LogTrace("Adding result with relevance {0}", relevance); }

                // We create a new citation for each result, event if it is in the very same document
                // this will simplify re-ranking
                var citation = new Citation();
                citations.Add(citation);

                // Add the partition to the list of citations
                citation.Index = userQuestion.UserQueryOptions.Index;
                citation.DocumentId = documentId;
                citation.FileId = fileId;
                citation.Link = linkToFile;
                citation.SourceContentType = memory.GetFileContentType(this._log);
                citation.SourceName = memory.GetFileName(this._log);
                citation.SourceUrl = memory.GetWebPageUrl();

                citation.Partitions.Add(new Citation.Partition
                {
                    Text = partitionText,
                    Relevance = (float)relevance,
                    PartitionNumber = memory.GetPartitionNumber(this._log),
                    SectionNumber = memory.GetSectionNumber(),
                    LastUpdate = memory.GetLastUpdate(),
                    Tags = memory.Tags,
                });
            }

            //ok now that you have all the memory record and citations, add to the object
            userQuestion.AddCitations("standard-vector-search", citations);
        }
    }
}
