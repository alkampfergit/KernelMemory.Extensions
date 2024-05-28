using Microsoft.KernelMemory.MemoryStorage;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace KernelMemory.Extensions;

internal class MemoryRecordEqualityComparer : IEqualityComparer<MemoryRecord>
{
    internal static MemoryRecordEqualityComparer Instance { get; } = new MemoryRecordEqualityComparer();

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
        //hash code must be equal for object that are considered equal
        return obj.GetDocumentId().GetHashCode() ^
            obj.GetPartitionNumber().GetHashCode() ^
            obj.GetFileId().GetHashCode();
    }
}
