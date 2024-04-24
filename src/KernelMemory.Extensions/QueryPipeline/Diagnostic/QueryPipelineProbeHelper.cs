using Microsoft.KernelMemory.MemoryStorage;
using System;

namespace KernelMemory.Extensions.QueryPipeline.Diagnostic
{
    /// <summary>
    /// Helps creating some standard probe helper to help evaluate.
    /// </summary>
    public static class QueryPipelineProbeHelper
    {
        /// <summary>
        /// Create an helper that looks for text inside the memory record, useful
        /// when your can validate the memory record based on the text it contains.
        /// </summary>
        /// <param name="contains"></param>
        /// <returns></returns>
        public static Func<MemoryRecord, bool> ForStringContains(string contains)
        {
            return (mr) => mr.GetPartitionText().Contains(contains, StringComparison.OrdinalIgnoreCase);
        }
    }
}
