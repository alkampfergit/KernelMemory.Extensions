using Microsoft.KernelMemory.AI;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TiktokenSharp;
using static KernelMemory.ElasticSearch.Anthropic.RawAnthropicClient;

namespace KernelMemory.ElasticSearch.Anthropic;

internal class AnthropicTextGeneration : ITextGenerator
{
    private readonly AnthropicTextGenerationConfiguration _config;
    private readonly RawAnthropicClient _client;

    private static readonly TikToken _tokenizer = TikToken.GetEncoding("cl100k_base");

    public AnthropicTextGeneration(
        IHttpClientFactory httpClientFactory,
        AnthropicTextGenerationConfiguration config)
    {
        _config = config;
        _client = new RawAnthropicClient(_config.ApiKey, httpClientFactory, _config.HttpClientName);
    }

    /// <inheritdoc />
    public int MaxTokenTotal => _config.MaxTokenTotal;

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        return _tokenizer.Encode(text).Count;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateTextAsync(
        string prompt,
        TextGenerationOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CallClaudeStreamingParams p = new CallClaudeStreamingParams
        {
            ModelName = _config.ModelName,
            System = "You are an assistant that will answer user query based on a context",
            Prompt = prompt,
            Temperature = options.Temperature,
            MaxTokens = options.MaxTokens ?? 2048
        };
        var streamedResponse = _client.CallClaudeStreaming(p);

        await foreach (var response in streamedResponse.WithCancellation(cancellationToken))
        {
            //now we simply yield the response
            switch (response)
            {
                case ContentBlockDelta blockDelta:
                    yield return blockDelta.Delta.Text;
                    break;
                default:
                    //do nothing we simple want to use delta text.
                    break;
            }
        }
    }
}