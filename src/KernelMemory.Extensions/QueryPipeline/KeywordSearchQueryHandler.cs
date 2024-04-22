using KernelMemory.ElasticSearch;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

public class KeywordSearchQueryHandler : BasicQueryHandler
{
    private readonly IAdvancedMemoryDb _advancedMemoryDb;
    private readonly ILogger<KeywordSearchQueryHandler> _log;

    public KeywordSearchQueryHandler(
        IAdvancedMemoryDb advancedMemoryDb,
        ILogger<KeywordSearchQueryHandler>? log = null)
    {
        _advancedMemoryDb = advancedMemoryDb;
        _log = log ?? DefaultLogger<KeywordSearchQueryHandler>.Instance;
    }

    public override string Name => nameof(KeywordSearchQueryHandler);

    protected override async Task OnHandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
    {
        //perform a simple and plain search on the advanced memory db, for now only elastic support this
        //interface
        var resultEnumerator = _advancedMemoryDb.SearchKeywordAsync(
            userQuestion.UserQueryOptions.Index,
            userQuestion.Question,
            filters: userQuestion.Filters,
            limit: 10,
            withEmbeddings: false);

        var citations = new List<Citation>();
        await foreach (var memory in resultEnumerator)
        {
            // Note: a document can be composed by multiple files
            string documentId = memory.GetDocumentId(_log);

            // Identify the file in case there are multiple files
            string fileId = memory.GetFileId(_log);

            // TODO: URL to access the file in content storage
            string linkToFile = $"{userQuestion.UserQueryOptions.Index}/{documentId}/{fileId}";

            var partitionText = memory.GetPartitionText(_log).Trim();
            if (string.IsNullOrEmpty(partitionText))
            {
                _log.LogError("The document partition is empty, doc: {0}", memory.Id);
                continue;
            }

            var citation = new Citation();
            citations.Add(citation);

            // Add the partition to the list of citations
            citation.Index = userQuestion.UserQueryOptions.Index;
            citation.DocumentId = documentId;
            citation.FileId = fileId;
            citation.Link = linkToFile;
            citation.SourceContentType = memory.GetFileContentType(_log);
            citation.SourceName = memory.GetFileName(_log);
            citation.SourceUrl = memory.GetWebPageUrl();

            citation.Partitions.Add(new Citation.Partition
            {
                Text = partitionText,
                Relevance = 0, //there is no absolute relevance in BM25, we let the reranker work.
                PartitionNumber = memory.GetPartitionNumber(_log),
                SectionNumber = memory.GetSectionNumber(),
                LastUpdate = memory.GetLastUpdate(),
                Tags = memory.Tags,
            });
        }

        //ok now that you have all the memory record and citations, add to the object
        userQuestion.AddCitations("standard-keyword-search", citations);
    }
}
