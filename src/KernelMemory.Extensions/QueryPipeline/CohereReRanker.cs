﻿using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.QueryPipeline;
using Microsoft.KernelMemory.MemoryStorage;
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

        public async Task<IReadOnlyCollection<MemoryRecord>> ReRankAsync(
            string question,
            IReadOnlyDictionary<string, IReadOnlyCollection<MemoryRecord>> candidates)
        {
            //Create array of citations.
            var allCitations = candidates.Values
                .SelectMany(c => c)
                .Distinct(new MemoryRecordEqualityComparer())
                .ToArray();

            //from distinct array of citations extract text for re-ranking.
            var documents = allCitations
                .Select(c => c.GetPartitionText() ?? "")
                .ToArray();

            //TODO: you need to chunk documents
            var reRankRequest = new CohereReRankRequest(question, documents);
            var result = await _rawCohereClient.ReRankAsync(reRankRequest);

            return result.Results.Select(d => allCitations[d.Index]).ToList();
        }
    }
}
