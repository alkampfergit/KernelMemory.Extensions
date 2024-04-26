using KernelMemory.Extensions.QueryPipeline;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

/// <summary>
/// This class represent user question and allows for more advanced scenario
/// like query-rewrite, query-expansion and other stuff.
/// </summary>
public class UserQuestion
{
    internal IReRanker? ReRanker { get; set; }

    /// <summary>
    /// Add current re-ranker used to re-rank the results.
    /// </summary>
    /// <param name="reRanker"></param>
    internal void AddReRanker(IReRanker reRanker)
    {
        ReRanker = reRanker;
    }

    public UserQueryOptions UserQueryOptions { get; }

    public string Question { get; }

    /// <summary>
    /// Optional list of filter to perform the query.
    /// </summary>
    public ICollection<MemoryFilter>? Filters { get; }

    public List<Question> ExpandedQuestions { get; }

    /// <summary>
    /// We can have more than one single way of search information, so we can
    /// have multiple list of memory records, each one returned with a different
    /// technique.
    /// </summary>
    public ConcurrentDictionary<string, IReadOnlyCollection<MemoryRecord>> MemoryRecordPool { get; } = new();

    /// <summary>
    /// Add a list of memory records for a specific source, you will overwrite entirely the 
    /// previous list of memory records if you use the very source name.
    /// </summary>
    /// <param name="sourceName"></param>
    /// <param name="memoryRecords"></param>
    public void AddMemoryRecordSource(string sourceName, IEnumerable<MemoryRecord> memoryRecords)
    {
        MemoryRecordPool[sourceName] = memoryRecords.ToArray();
        //Invalidate the memory records, we need to rerank;
        _orderedMemoryRecords = null;
    }

    private IReadOnlyCollection<MemoryRecord>? _orderedMemoryRecords;

    /// <summary>
    /// This method will return all memory records ordered and re-ranked to be used in the
    /// G part of the RAG.
    /// </summary>
    public async Task<IReadOnlyCollection<MemoryRecord>> GetMemoryOrdered()
    {
        return _orderedMemoryRecords ??= await ReRankAsync();
    }

    /// <summary>
    /// This is the final citation set by the andler when the answer is ok. It is usually set
    /// by the handler that set answer.
    /// </summary>
    public IReadOnlyCollection<Citation>? Citations { get; set; }

    /// <summary>
    /// If some error occurred we have the error here.
    /// </summary>
    public string? Errors { get; internal set; }

    private async Task<IReadOnlyCollection<MemoryRecord>> ReRankAsync()
    {
        if (MemoryRecordPool.IsEmpty)
        {
            return [];
        }
        if (MemoryRecordPool.Count == 1)
        {
            return [.. MemoryRecordPool.First().Value];
        }

        if (ReRanker == null)
        {
            throw new KernelMemoryException($"We have more than one Source memory records, source memory records are {MemoryRecordPool.Count} but we have no re-ranker");
        }
        return await ReRanker.ReRankAsync(this.Question, MemoryRecordPool);
    }

    /// <summary>
    /// This is the Answer to the question, when an handler popuplate this field
    /// it means that the question has been answered.
    /// </summary>
    public string? Answer { get; set; }

    public bool Answered => !string.IsNullOrEmpty(Answer);

    /// <summary>
    /// The <see cref="IQueryHandler"/> that answered the question
    /// </summary>
    public string? AnswerHandler { get; internal set; }

    public UserQuestion(
        UserQueryOptions userQueryOptions,
        string question,
        ICollection<MemoryFilter>? filters = null)
    {
        UserQueryOptions = userQueryOptions;
        Question = question;
        Filters = filters;
        ExpandedQuestions = [];
    }
}

public class UserQueryOptions
{
    public UserQueryOptions(string index)
    {
        Index = index;
    }

    public double MinRelevance { get; set; }

    public string Index { get; private set; }

    public int RetrievalQueryLimit { get; internal set; } = 10;
}

public record Question(string Text);