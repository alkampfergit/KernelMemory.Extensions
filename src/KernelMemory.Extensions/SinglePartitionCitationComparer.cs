using Microsoft.KernelMemory;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace KernelMemory.Extensions;

internal class SinglePartitionCitationComparer : IEqualityComparer<Citation>
{
    public bool Equals(Citation? x, Citation? y)
    {
        if (x == null || y == null)
        {
            return false;
        }
        return x?.FileId == y?.FileId && x.Partitions.FirstOrDefault()?.PartitionNumber == y.Partitions.FirstOrDefault()?.PartitionNumber;
    }

    public int GetHashCode([DisallowNull] Citation obj)
    {
        return obj.FileId.GetHashCode();
    }
}
