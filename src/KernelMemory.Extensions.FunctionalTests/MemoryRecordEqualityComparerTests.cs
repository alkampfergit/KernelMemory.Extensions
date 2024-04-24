using KernelMemory.Extensions.FunctionalTests.TestUtilities;
using Microsoft.KernelMemory.MemoryStorage;

namespace KernelMemory.Extensions.FunctionalTests
{
    public class MemoryRecordEqualityComparerTests
    {
        [Fact]
        public void Can_compare_memory_record_for_equality()
        {
            var mr1 = MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 1, "text1");
            var mr2 = MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 1, "text1");

            var comparer = new MemoryRecordEqualityComparer();
            Assert.True(comparer.Equals(mr1, mr2));
        }

        [Fact]
        public void Compare_consider_partition_text()
        {
            var mr1 = MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 1, "text1");
            var mr2 = MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 2, "text1");

            var comparer = new MemoryRecordEqualityComparer();
            Assert.False(comparer.Equals(mr1, mr2));
        }

        [Fact]
        public void Can_use_comparer_to_use_linq_distinct()
        {
            var list = new List<MemoryRecord>
            {
                MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 1, "text1"),
                MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 1, "text1"),
                MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 2, "text1"),
                MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 2, "text1"),
            };

            var comparer = new MemoryRecordEqualityComparer();
            var distinct = list.Distinct(comparer).ToList();
            Assert.Equal(2, distinct.Count);
        }
    }
}
