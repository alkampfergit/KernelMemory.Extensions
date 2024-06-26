﻿using KernelMemory.Extensions.QueryPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Cohere;

public class CohereCommandRQueryExecutorConfiguration
{
    /// <summary>
    /// <para>
    /// In this version we limit the number of memory records that can be used in the query.
    /// It would be better to use a tokenizer to split the text in chunks and then specify
    /// token limits but we do not have a local tokenizer.
    /// Local tokenizer exists here https://huggingface.co/Cohere/Command-nightly as for the
    /// documentation https://docs.cohere.com/docs/tokens-and-tokenizers but we need python.
    /// </para>
    /// <para>
    /// You can try using TikToken (BGP tokenizer) using the model
    /// https://storage.googleapis.com/cohere-public/tokenizers/command-r-plus.json
    /// </para>
    /// </summary>
    public int MaxMemoryRecord { get; set; } = 5;

    /// <summary>
    /// Temperature used to perform RAG query.
    /// </summary>
    public float Temperature { get; set; } = 0.0f;

    /// <summary>
    /// If we have no citations, we can tell that we do not have an answer
    /// because if no document is pertinent, we should ground the answer
    /// and avoid allucination. We can change this behavior with this settings.
    /// </summary>
    public bool RemoveAnswerIfNoCitations { get; set; } = true;
}

public class CohereCommandRQueryExecutor : BasicAsyncQueryHandlerWithProgress
{
    public override string Name => "CoereCommandRagQueryExecutor";

    private readonly RawCohereClient _rawCohereClient;
    private readonly CohereCommandRQueryExecutorConfiguration _config;
    private readonly ILogger<StandardRagQueryExecutor> _log;

    public CohereCommandRQueryExecutor(
        RawCohereClient rawCohereClient,
        CohereCommandRQueryExecutorConfiguration config,
        ILogger<StandardRagQueryExecutor>? log = null)
    {
        _rawCohereClient = rawCohereClient;
        _config = config;
        _log = log ?? DefaultLogger<StandardRagQueryExecutor>.Instance;
    }

    protected override async IAsyncEnumerable<UserQuestionProgress> OnHandleStreamingAsync(
        UserQuestion userQuestion,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var memoryRecords = await userQuestion.GetMemoryOrdered();
        if (memoryRecords.Count == 0)
        {
            //Well we have no memory we can simply return. 
            yield break;
        }

        var usableMemoryRecords = memoryRecords.Take(_config.MaxMemoryRecord).ToList();

        //ok simply create the request to pass to cohere chat request.
        var cohereRagRequest = CohereRagRequest.CreateFromMemoryRecord(userQuestion.Question, usableMemoryRecords);
        cohereRagRequest.Temperature = _config.Temperature;
        var asiterator = _rawCohereClient.RagQueryStreamingAsync(cohereRagRequest);

        //ok we need to update citations.
        List<CohereRagStreamingResponse> responses = [];
        StringBuilder answer = new();
        await foreach (var x in asiterator.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            responses.Add(x);
            //ok we need to check type of the iteration
            if (x.ResponseType == CohereRagResponseType.Text)
            {
                answer.Append(x.Text);
                yield return new UserQuestionProgress(UserQuestionProgressType.AnswerPart, x.Text);
            }
        }

        userQuestion.Answer = answer.ToString();

        HashSet<int> usedMemoryRecordIds = responses
            .Where(r => r.ResponseType == CohereRagResponseType.Citations)
            .SelectMany(r => r.Citations)
            .SelectMany(c => c.DocumentIds)
            .Select(c => int.Parse(c.Substring("doc_".Length)))
            .ToHashSet();

        var extCitations = new List<(int doc, string citation)>();
        foreach (var citationMessages in responses.Where(r => r.ResponseType == CohereRagResponseType.Citations))
        {
            foreach (var citation in citationMessages.Citations)
            {
                extCitations.AddRange(
                    citation.DocumentIds.Select(d => (int.Parse(d.Substring("doc_".Length)), citation.Text)));
                //TODO: we can have more information, like keyword used for citations. 
            }
        }

        userQuestion.ExtendedCitation = extCitations
            .GroupBy(c => c.doc)
            .Where(c => c.Key < usableMemoryRecords.Count)
            .Select(c => new { Group = c, MemoryRecord = usableMemoryRecords[c.Key] })
            .Select(c => new ExtendedCitation(c.MemoryRecord.Id, c.MemoryRecord.GetFileId(), c.Group.Select(g => g.citation).ToList()))
            .ToList();

        List<MemoryRecord> usedMemoryRecord = usableMemoryRecords
            .Where((_, i) => usedMemoryRecordIds.Contains(i))
            .ToList();

        // now we need to clean up the citations, including only the one used to answer the question
        userQuestion.Citations = MemoryRecordHelper.BuildCitations(usedMemoryRecord, userQuestion.UserQueryOptions.Index, this._log);

        // ground if needed
        if (_config.RemoveAnswerIfNoCitations && extCitations.Count == 0)
        {
            //no answer is possible, because we do not have citations.
            userQuestion.Answer = "";
        }
    }
}
