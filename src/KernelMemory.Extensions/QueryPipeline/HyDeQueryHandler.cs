using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.SemanticKernel;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

public class HiDeQueryHandlerConfiguration 
{
    public string Prompt { get; set; } = "Given a question, generate a paragraph of text that answers the question.";
}

public class HyDeQueryHandler : BasicQueryHandler
{
    private readonly IMemoryDb _memoryDb;
    private readonly Kernel _kernel;
    private readonly HiDeQueryHandlerConfiguration _configuration;
    private readonly ILogger<StandardVectorSearchQueryHandler> _log;

    public override string Name => "StandardVectorSearchQueryHandler";

    public HyDeQueryHandler(
        IMemoryDb memory,
        Kernel kernel,
        HiDeQueryHandlerConfiguration? configuration,
        ILogger<StandardVectorSearchQueryHandler>? log = null)
    {
        _memoryDb = memory;
        _kernel = kernel;
        _configuration = configuration ?? new HiDeQueryHandlerConfiguration();
        _log = log ?? DefaultLogger<StandardVectorSearchQueryHandler>.Instance;
    }

    /// <summary>
    /// Perform a vector search in default memory using the hyde principle.
    /// </summary>
    /// <param name="userQuestion"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async Task OnHandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
    {
        // Perform a vector search in default memory
        StringBuilder prompt = new StringBuilder(_configuration.Prompt.Length + userQuestion.Question.Length + "Question: ".Length + "Paragraph: ".Length + 20);
        prompt.AppendLine(_configuration.Prompt);
        if (!string.IsNullOrEmpty(userQuestion.Context))
        {
            prompt.AppendLine("The context of the question is: " + userQuestion.Context);
        }

        prompt.AppendLine("Question: " + userQuestion.Question);
        prompt.AppendLine("Paragraph: ");

        var result = await _kernel.InvokePromptAsync(prompt.ToString(), cancellationToken: cancellationToken);

        var paragraph = result.ToString();

        var list = new List<(MemoryRecord memory, double relevance)>();

        IAsyncEnumerable<(MemoryRecord, double)> matches = this._memoryDb.GetSimilarListAsync(
            index: userQuestion.UserQueryOptions.Index,
            text: paragraph,
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
        userQuestion.AddMemoryRecordSource("hyde-vector-search", records);
    }
}
