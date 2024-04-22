using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Prompts;
using Microsoft.KernelMemory.Search;

namespace SemanticMemory.Extensions
{
    /// <summary>
    /// Enhanced Kernel memory search client to allow for more advanced search capabilities.
    /// </summary>
    public class KernelMemorySearchClient
    {
        private readonly IMemoryDb _memoryDb;
        private readonly ITextGenerator _textGenerator;
        private readonly SearchClientConfig? _config;
        private readonly IPromptProvider? _promptProvider;
        private readonly ILogger<SearchClient>? _log;

        public KernelMemorySearchClient(
            IMemoryDb memoryDb,
            ITextGenerator textGenerator,
            SearchClientConfig? config = null,
            IPromptProvider? promptProvider = null,
            ILogger<SearchClient>? log = null)
        {
            _memoryDb = memoryDb;
            _textGenerator = textGenerator;
            _config = config;
            _promptProvider = promptProvider;
            _log = log;
        }
    }
}
