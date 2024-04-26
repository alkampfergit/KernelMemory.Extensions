using Microsoft.KernelMemory.MemoryStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.QueryPipeline.Diagnostic
{
    /// <summary>
    /// Simple class that can perform basic analysis on memory returned
    /// from the storage and reranking capabilities of the pipeline.
    /// </summary>
    public class QueryPipelineProbe
    {
        public QueryPipelineProbe(Func<MemoryRecord, bool> memoryValidatorFunc)
        {
            MemoryValidatorFunc = memoryValidatorFunc;
        }

        public Func<MemoryRecord, bool> MemoryValidatorFunc { get; private set; }

        /// <summary>
        /// Perform analysis.
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        public async ValueTask<QueryPipelineStatistics> AnalyzePipelineAsync(UserQuestion question)
        {
            //ok we need to find all memory record before reranking that are ok
            QueryPipelineStatistics queryPipelineStatistics = new QueryPipelineStatistics();
            queryPipelineStatistics.RetrieveStats = new Dictionary<string, RetrieverStats>();
            foreach (var mrList in question.MemoryRecordPool)
            {
                //For each single record run the validator and return only the indexes that are valid
                bool[] validElements = mrList.Value.Select(MemoryValidatorFunc).ToArray();
                queryPipelineStatistics.RetrieveStats.Add(
                   mrList.Key,
                    new RetrieverStats
                        {
                            TotalRecords = mrList.Value.Count,
                            ValidRecords = validElements.Count(r => r),
                            ValidRecordsIndices = validElements.Select((r, i) => r ? i : -1).Where(r => r >= 0).ToArray()
                        }
                 );
            }

            //then we need to grab reranked elements.
            var reranked = await question.GetMemoryOrdered();
            //now generated a retriever stats for the reranked elements
            queryPipelineStatistics.AfterReranking = new RetrieverStats
            {
                TotalRecords = reranked.Count,
                ValidRecords = reranked.Count(MemoryValidatorFunc),
                ValidRecordsIndices = reranked.Select((r, i) => MemoryValidatorFunc(r) ? i : -1).Where(r => r >= 0).ToArray()
            };

            return queryPipelineStatistics;
        }
    }

    /// <summary>
    /// Allow to evaluate how well a RAG pipeline is performing.
    /// </summary>
    public struct QueryPipelineStatistics
    {
        public Dictionary<string, RetrieverStats> RetrieveStats { get; internal set; }

        public RetrieverStats AfterReranking { get; set; }
    }

    public struct RetrieverStats
    {
        public int TotalRecords { get; internal set; }
        public int ValidRecords { get; internal set; }
        public int[] ValidRecordsIndices { get; internal set; }
    }
}
