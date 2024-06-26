using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Cohere;

public class RawCohereClient
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RawCohereClient> _log;
    private readonly string? _keyedName;

    public RawCohereClient(
        IServiceProvider serviceProvider,
        string? keyedName = null,
        ILogger<RawCohereClient>? log = null)
    {
        this._serviceProvider = serviceProvider;
        _log = log ?? DefaultLogger<RawCohereClient>.Instance;
        _keyedName = keyedName;
    }

    private T CreateClient<T>() where T : notnull
    {
        if (string.IsNullOrEmpty(_keyedName))
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        return _serviceProvider.GetRequiredKeyedService<T>(_keyedName);
    }

    /// <summary>
    /// https://docs.cohere.com/reference/rerank
    /// </summary>
    public Task<ReRankResult> ReRankAsync(
        CohereReRankRequest reRankRequest,
        CancellationToken cancellationToken = default)
    {
        if (!reRankRequest.Answers.Any())
        {
            //Caller does not specify any answer, we have nothing to reorder.
            return Task.FromResult(ReRankResult.Empty);
        }

        var client = CreateClient<RawCohereReRankerClient>();

        return client.ReRankAsync(reRankRequest, cancellationToken);
    }

    /// <summary>
    /// https://docs.cohere.com/docs/retrieval-augmented-generation-rag
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public Task<CohereRagResponse> RagQueryAsync(
        CohereRagRequest cohereRagRequest,
        CancellationToken cancellationToken = default)
    {
        if (cohereRagRequest is null)
        {
            throw new ArgumentNullException(nameof(cohereRagRequest));
        }

        if (cohereRagRequest.Stream)
        {
            throw new NotImplementedException("Streaming is not supported, please use RagQueryStreamingAsync");
        }

        var client = CreateClient<RawCohereChatClient>();

        return client.RagQueryAsync(cohereRagRequest, cancellationToken);
    }

    /// <summary>
    /// https://docs.cohere.com/docs/retrieval-augmented-generation-rag
    /// https://docs.cohere.com/docs/streaming
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    public IAsyncEnumerable<CohereRagStreamingResponse> RagQueryStreamingAsync(
        CohereRagRequest cohereRagRequest,
        CancellationToken cancellationToken = default)
    {
        if (cohereRagRequest is null)
        {
            throw new ArgumentNullException(nameof(cohereRagRequest));
        }

        var client = CreateClient<RawCohereChatClient>();
        return client.RagQueryStreamingAsync(cohereRagRequest, cancellationToken);
    }

    public Task<EmbedResult> EmbedAsync(
        CohereEmbedRequest embedRequest,
        CancellationToken cancellationToken = default)
    {
        if (embedRequest.Texts == null || !embedRequest.Texts.Any())
        {
            throw new ArgumentException("Texts array cannot be null or empty", nameof(embedRequest.Texts));
        }

        var client = CreateClient<RawCohereEmbeddingClient>();

        return client.EmbedAsync(embedRequest, cancellationToken);
    }
}
