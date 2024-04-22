using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace KernelMemory.Extensions;

internal class MemoryRecordEqualityComparer : IEqualityComparer<MemoryRecord>
{
    public bool Equals(MemoryRecord? x, MemoryRecord? y)
    {
        if (x == null || y == null)
        {
            return false;
        }
        return x?.GetDocumentId() == y?.GetDocumentId()
            && x?.GetPartitionNumber() == y?.GetPartitionNumber()
            && x?.GetFileId() == y?.GetFileId();
    }

    public int GetHashCode([DisallowNull] MemoryRecord obj)
    {
        return obj.GetHashCode();
    }
}
