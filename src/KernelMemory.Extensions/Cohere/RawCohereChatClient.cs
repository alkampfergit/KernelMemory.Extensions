using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KernelMemory.Extensions.Helper;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;

namespace KernelMemory.Extensions.Cohere;

public class RawCohereChatClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RawCohereChatClient> _log;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public RawCohereChatClient(
        CohereChatConfiguration config,
        HttpClient httpClient,
        ILogger<RawCohereChatClient>? log = null)
    {
        if (String.IsNullOrEmpty(config.ApiKey))
        {
            throw new ArgumentException("ApiKey is required", nameof(config.ApiKey));
        }

        this._httpClient = httpClient;
        _log = log ?? DefaultLogger<RawCohereChatClient>.Instance;
        _apiKey = config.ApiKey;
        _baseUrl = config.BaseUrl;
    }

    /// <summary>
    /// https://docs.cohere.com/docs/retrieval-augmented-generation-rag
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<CohereRagResponse> RagQueryAsync(
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

        var client = _httpClient;

        string jsonPayload = HttpClientPayloadSerializerHelper.Serialize(cohereRagRequest);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/v1/chat")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        //now perform the request.
        if (!response.IsSuccessStatusCode)
        {
            var responseError = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to send request: {response.StatusCode} - {responseError}");
        }
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<CohereRagResponse>(responseString)!;
    }

    /// <summary>
    /// https://docs.cohere.com/docs/retrieval-augmented-generation-rag
    /// https://docs.cohere.com/docs/streaming
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public async IAsyncEnumerable<CohereRagStreamingResponse> RagQueryStreamingAsync(
        CohereRagRequest cohereRagRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cohereRagRequest is null)
        {
            throw new ArgumentNullException(nameof(cohereRagRequest));
        }

        var client = _httpClient;
        //force streaming
        cohereRagRequest.Stream = true;

        string jsonPayload = HttpClientPayloadSerializerHelper.Serialize(cohereRagRequest);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/v1/chat")
        {
            Content = content,
        };
        request.Headers.Add("Authorization", $"bearer {_apiKey}");

        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        //now perform the request.
        if (!response.IsSuccessStatusCode)
        {
            var responseError = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to send request: {response.StatusCode} - {responseError}");
        }

        response.EnsureSuccessStatusCode();
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        using (StreamReader reader = new(responseStream))
        {
            while (!reader.EndOfStream)
            {
                string line = (await reader.ReadLineAsync(cancellationToken))!;
                var data = JsonSerializer.Deserialize<ChatStreamEvent>(line)!;

                if (data.EventType == "stream-start" || data.EventType == "stream-end" || data.EventType == "search-results")
                {
                    //not interested in this events
                    continue;
                }

                if (data.EventType == "text-generation")
                {
                    yield return new CohereRagStreamingResponse()
                    {
                        Text = data.Text,
                        ResponseType = CohereRagResponseType.Text,
                    };
                }
                else if (data.EventType == "citation-generation")
                {
                    yield return new CohereRagStreamingResponse()
                    {
                        Citations = data.Citations,
                        ResponseType = CohereRagResponseType.Citations
                    };
                }
                else
                {
                    //not supported.
                    _log.LogWarning("Cohere stream api receved unknown event data type {0}", data.EventType);
                }
            }
        }
    }
}
