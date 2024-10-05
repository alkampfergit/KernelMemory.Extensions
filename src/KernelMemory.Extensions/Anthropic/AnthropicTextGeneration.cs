using Microsoft.KernelMemory.AI;
using Microsoft.ML.Tokenizers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.ElasticSearch.Anthropic;

internal class AnthropicTextGeneration : ITextGenerator
{
    private readonly RawAnthropicHttpClient _rawAnthropicHttpClient;
    private readonly AnthropicTextGenerationConfiguration _config;

    /// <summary>
    /// We do not have cohere tokenizer directly in C# - in this first version we use gpt4 tokenizer
    /// and we know that this is a raw approximation but we only need to count.
    /// </summary>
    private static readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

    public AnthropicTextGeneration(
        RawAnthropicHttpClient rawAnthropicHttpClient,
        AnthropicTextGenerationConfiguration config)
    {
        _rawAnthropicHttpClient = rawAnthropicHttpClient;
        _config = config;
    }

    /// <inheritdoc />
    public int MaxTokenTotal => _config.MaxTokenTotal;

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        return _tokenizer.CountTokens(text);
    }

    public IReadOnlyList<string> GetTokens(string text)
    {
        var tokens = _tokenizer.EncodeToTokens(text, out var normalizedString);
        return tokens.Select(t => t.Value).ToArray();
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
        var streamedResponse = _rawAnthropicHttpClient.CallClaudeStreaming(p);

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