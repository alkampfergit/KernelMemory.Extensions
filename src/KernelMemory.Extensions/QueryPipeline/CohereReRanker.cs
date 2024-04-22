using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.QueryPipeline;
using Microsoft.KernelMemory;
using SemanticMemory;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KernelMemory.Extensions
{
    public class CohereReRanker : IReRanker
    {
        private readonly RawCohereClient _rawCohereClient;

        public CohereReRanker(RawCohereClient rawCohereClient)
        {
            _rawCohereClient = rawCohereClient;
        }

        public async Task<IReadOnlyCollection<Citation>> ReRankAsync(
            string question,
            IReadOnlyDictionary<string, IReadOnlyCollection<Citation>> citations)
        {
            //Create array of citations.
            var allCitations = citations.Values
                .SelectMany(c => c.Where(c => c.Partitions.Count > 0))
                .Distinct(new SinglePartitionCitationComparer())
                .ToArray();

            //from distinct array of citations extract text for re-ranking.
            var documents = allCitations
                .Select(c => c.Partitions.First().Text)
                .ToArray();

            //TODO: you need to chunk documents
            var reRankRequest = new CohereReRankRequest(question, documents);
            var result = await _rawCohereClient.ReRankAsync(reRankRequest);

            return result.Results.Select(d => allCitations[d.Index]).ToList();
        }
    }
}
