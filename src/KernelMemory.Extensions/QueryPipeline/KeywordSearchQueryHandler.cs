using KernelMemory.ElasticSearch;
using KernelMemory.Extensions.Interfaces;
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
    private readonly IKernelMemoryExtensionMemoryDb _advancedMemoryDb;
    private readonly ILogger<KeywordSearchQueryHandler> _log;

    public KeywordSearchQueryHandler(
        IKernelMemoryExtensionMemoryDb advancedMemoryDb,
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

        var memoryRecords = new List<MemoryRecord>();
        await foreach (var memory in resultEnumerator)
        {
            memoryRecords.Add(memory);
        }

        //ok now that you have all the memory record and citations, add to the object
        userQuestion.AddMemoryRecordSource("standard-keyword-search", memoryRecords);
    }
}
