using KernelMemory.Extensions.LocalLlm;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SemanticMemory.Helper;

public class LmStudioTextGeneration : ITextGenerator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri _baseUri;
    private readonly DefaultGPTTokenizer _textTokenizer;

    public LmStudioTextGeneration(
        IHttpClientFactory httpClientFactory,
        Uri baseUri,
        int maxToken = 2048)
    {
        _httpClientFactory = httpClientFactory;
        _baseUri = baseUri;

        _textTokenizer = new DefaultGPTTokenizer();
        MaxTokenTotal = maxToken;
    }

    /// <inheritdoc />
    public int MaxTokenTotal { get; }

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        return _textTokenizer.CountTokens(text);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateTextAsync(
        string prompt,
        TextGenerationOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        //do a post request to the model to get the dense vector size
        var body = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = options.Temperature,
            max_tokens = options.MaxTokens,
            stream = false
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUri.AbsoluteUri.TrimEnd('/')}/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        //TODO: Proper error handling
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get dense vector size");
        }
        var responseStream = response.Content.ReadAsStream();
        var completions = JsonSerializer.Deserialize<ApiResponse>(responseStream);

        foreach (var choice in completions.Choices)
        {
            yield return choice.Message.Content;
        }
    }
}
