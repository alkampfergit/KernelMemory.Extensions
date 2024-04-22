using Microsoft.KernelMemory;
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
        /// of <see cref="Citation"/> and it can also perform deduplication.
        /// </summary>
        /// <param name="question">The original question made by the user.</param>
        /// <param name="citations">List of the original citations.</param>
        /// <returns></returns>
        Task<IReadOnlyCollection<Citation>> ReRankAsync(string question, IReadOnlyDictionary<string, IReadOnlyCollection<Citation>> citations);
    }

    /// <summary>
    /// This is the basic form of re-ranker, it is really not a re-ranker, it will start taking element one by one
    /// from all the sources.
    /// </summary>
    public class BaseReRanker : IReRanker
    {
        public Task<IReadOnlyCollection<Citation>> ReRankAsync(string question, IReadOnlyDictionary<string, IReadOnlyCollection<Citation>> citations)
        {
            if (citations.Count == 0)
            {
                return Task.FromResult<IReadOnlyCollection<Citation>>(new List<Citation>());
            }

            if (citations.Count == 1)
            {
                return Task.FromResult(citations.Single().Value);
            }

            //ok will perform a stupid reranking
            List<Citation> retValue = new List<Citation>();
            var allCitationsList = citations.Values.ToList();
            var maxLen = allCitationsList.Max(x => x.Count);
            for (int i = 0; i < maxLen; i++)
            {
                for (int j = 0; j < allCitationsList.Count; j++)
                {
                    if (i < allCitationsList[j].Count)
                    {
                        retValue.Add(allCitationsList[j].ElementAt(i));
                    }
                }
            }

            return Task.FromResult<IReadOnlyCollection<Citation>>(retValue.AsReadOnly());
        }
    }
}
