using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Linq;

namespace KernelMemory.Extensions.QueryPipeline
{
    internal static class MemoryRecordHelper
    {
        internal static IReadOnlyCollection<Citation>? BuildCitations(
            List<MemoryRecord> usedMemoryRecord,
            string index,
            ILogger logger)
        {
            var result = new List<Citation>();
            // Memories are sorted by relevance, starting from the most relevant
            foreach (MemoryRecord memory in usedMemoryRecord)
            {
                // Note: a document can be composed by multiple files
                string documentId = memory.GetDocumentId(logger);

                // Identify the file in case there are multiple files
                string fileId = memory.GetFileId(logger);

                // TODO: URL to access the file in content storage
                string linkToFile = $"{index}/{documentId}/{fileId}";

                var partitionText = memory.GetPartitionText(logger).Trim();
                if (string.IsNullOrEmpty(partitionText))
                {
                    logger.LogError("The document partition is empty, doc: {0}", memory.Id);
                    continue;
                }

                // If the file is already in the list of citations, only add the partition
                var citation = result.FirstOrDefault(x => x.Link == linkToFile);
                if (citation == null)
                {
                    citation = new Citation();
                    result.Add(citation);
                }

                // Add the partition to the list of citations
                citation.Index = index;
                citation.DocumentId = documentId;
                citation.FileId = fileId;
                citation.Link = linkToFile;
                citation.SourceContentType = memory.GetFileContentType(logger);
                citation.SourceName = memory.GetFileName(logger);
                citation.SourceUrl = memory.GetWebPageUrl();

                citation.Partitions.Add(new Citation.Partition
                {
                    Text = partitionText,
                    Relevance = 0, //we cannot have relevance with mixed sources. relevance has no meaing.
                    PartitionNumber = memory.GetPartitionNumber(logger),
                    SectionNumber = memory.GetSectionNumber(),
                    LastUpdate = memory.GetLastUpdate(),
                    Tags = memory.Tags,
                });
            }

            return result;
        }
    }
}
