using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.QueryPipeline
{
    /// <summary>
    /// A simple interface that will re-rank multiple results of queries. It will 
    /// receive a list of all source citations and will give each one a new rank.
    /// For each <see cref="Citation"/> it will assign a new rank based on a different
    /// algorithm.
    /// </summary>
    public interface IReRanker
    {
        /// <summary>
        /// Accept the dictionary of source citations, and then will return an ordered list
        /// of <see cref="MemoryRecord"/> and it can also perform deduplication.
        /// </summary>
        /// <param name="question">The original question made by the user.</param>
        /// <param name="candidates">List of the original candidates.</param>
        /// <returns></returns>
        Task<IReadOnlyCollection<MemoryRecord>> ReRankAsync(string question, IReadOnlyDictionary<string, IReadOnlyCollection<MemoryRecord>> candidates);
    }

    /// <summary>
    /// This is the basic form of re-ranker, it is really not a re-ranker, it will start taking element one by one
    /// from all the sources.
    /// </summary>
    public class BaseReRanker : IReRanker
    {
        public Task<IReadOnlyCollection<MemoryRecord>> ReRankAsync(string question, IReadOnlyDictionary<string, IReadOnlyCollection<MemoryRecord>> candidates)
        {
            if (candidates.Count == 0)
            {
                return Task.FromResult<IReadOnlyCollection<MemoryRecord>>(Array.Empty<MemoryRecord>());
            }

            if (candidates.Count == 1)
            {
                return Task.FromResult(candidates.Single().Value);
            }

            //ok will perform a stupid reranking taking one element from every source
            List<MemoryRecord> retValue = new List<MemoryRecord>();
            var allMemoryList = candidates.Values.ToList();
            var maxLen = allMemoryList.Max(x => x.Count);
            var equalityComparer = MemoryRecordEqualityComparer.Instance;
            for (int i = 0; i < maxLen; i++)
            {
                for (int j = 0; j < allMemoryList.Count; j++)
                {
                    if (i < allMemoryList[j].Count)
                    {
                        //check for deduplication
                        if (!retValue.Contains(allMemoryList[j].ElementAt(i), equalityComparer))
                        {
                            retValue.Add(allMemoryList[j].ElementAt(i));
                        }
                    }
                }
            }

            return Task.FromResult<IReadOnlyCollection<MemoryRecord>>(retValue.AsReadOnly());
        }
    }
}
