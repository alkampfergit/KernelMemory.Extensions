using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;

namespace KernelMemory.Extensions.FunctionalTests.TestUtilities
{
    internal static class MemoryRecordTestUtilities
    {
        internal static MemoryRecord CreateMemoryRecord(string documentId, string fileId, int partitionNumber, string textPartition)
        {
            var mr = new MemoryRecord();
            mr.Payload = new Dictionary<string, object>();
            mr.Payload["text"] = textPartition;
            mr.Tags = new TagCollection
            {
                { Constants.ReservedDocumentIdTag, documentId },
                { Constants.ReservedFileIdTag, fileId },
                { Constants.ReservedFilePartitionNumberTag, partitionNumber.ToString() }
            };

            return mr;
        }
    }
}
