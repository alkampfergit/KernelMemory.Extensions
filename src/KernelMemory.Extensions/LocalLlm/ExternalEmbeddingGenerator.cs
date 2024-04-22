using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.LocalLlm;

public class ExternalEmbeddingGenerator : ITextEmbeddingGenerator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExternalEmbeddingGeneratorConfig _embeddingGeneratorConfig;
    private readonly ILogger<ExternalEmbeddingGenerator> _log;
    private int _countTokenCallNum = 0;
    private int _countEmbeddingCallNum = 0;

    public ExternalEmbeddingGenerator(
        IHttpClientFactory httpClientFactory,
        ExternalEmbeddingGeneratorConfig embeddingGeneratorConfig)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingGeneratorConfig = embeddingGeneratorConfig;

        _log = DefaultLogger<ExternalEmbeddingGenerator>.Instance;

        ScanModel();
    }

    private void ScanModel()
    {
        var client = _httpClientFactory.CreateClient();
        //do a post request to the model to get the dense vector size
        var body = new DimensionInput(_embeddingGeneratorConfig.ModelName);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_embeddingGeneratorConfig.Address.TrimEnd('/')}/dimensions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        var response = client.Send(request);

        //TODO: Proper error handling
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get dimensions");
        }
        var responseStream = response.Content.ReadAsStream();
        var dimensionResult = JsonSerializer.Deserialize<DimensionResult>(responseStream);
        VectorSize = dimensionResult.dimension;
        MaxTokens = dimensionResult.maxSequenceLength;
    }

    /// <inheritdoc />
    public int MaxTokens { get; private set; } = 384;

    public int VectorSize { get; private set; } = 768;

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        var client = _httpClientFactory.CreateClient();
        //do a post request to the model to get the dense vector size
        var body = new
        {
            modelName = _embeddingGeneratorConfig.ModelName,
            text
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_embeddingGeneratorConfig.Address.TrimEnd('/')}/count-tokens")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        var response = client.Send(request);

        //TODO: Proper error handling
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get dense vector size");
        }
        var responseStream = response.Content.ReadAsStream();
        var tokenCountResult = JsonSerializer.Deserialize<TokenCountResult>(responseStream);
        _log.LogDebug("[{countnum}]Count Token of text of len {textlength} token number {tokencount}", Interlocked.Increment(ref _countTokenCallNum).ToString().PadLeft(8, ' '), text.Length, tokenCountResult.count);
        return tokenCountResult.count;
    }

    /// <inheritdoc />
    public async Task<Embedding> GenerateEmbeddingAsync(
        string text, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        //do a post request to the model to get the dense vector size
        var body = new SentenceInput(new List<string> { text }, _embeddingGeneratorConfig.ModelName);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_embeddingGeneratorConfig.Address.TrimEnd('/')}/process-sentences")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request, cancellationToken);

        //TODO: Proper error handling
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get embedding");
        }
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var embeddingResult = JsonSerializer.Deserialize<EmbeddingModel>(responseStream);
        var embeddingArray = embeddingResult.embeddings.FirstOrDefault();
        if (embeddingArray == null)
        {
            throw new Exception("Failed to get embedding");
        }

        _log.LogInformation("[{countnum}] Embedding generated for text of len {textlength} ", Interlocked.Increment(ref _countEmbeddingCallNum).ToString().PadLeft(8, ' '), text.Length);

        return new Embedding(embeddingArray.Select(d => (float)d).ToArray());
    }

    public record SentenceInput(List<string> sentences, string modelName);
    public record DimensionInput(string modelName);
    public record DimensionResult(string model, int maxSequenceLength, int dimension);
    public record EmbeddingModel(List<double[]> embeddings, string model);

    private record EmbeddingInfo(string model, int dimension);
    private record TokenCountResult(int count);
}

public class ExternalEmbeddingGeneratorConfig
{
    public string ModelName { get; set; } = "all-mpnet-base-v2";
    public string Address { get; set; } = "http://localhost:8000";
}
