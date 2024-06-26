using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KernelMemory.Extensions.Helper;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;

namespace KernelMemory.Extensions.Cohere;

public class RawCohereEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RawCohereChatClient> _log;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public RawCohereEmbeddingClient(
        CohereEmbedConfiguration config,
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

    public async Task<EmbedResult> EmbedAsync(
        CohereEmbedRequest embedRequest,
        CancellationToken cancellationToken = default)
    {
        if (embedRequest.Texts == null || !embedRequest.Texts.Any())
        {
            throw new ArgumentException("Texts array cannot be null or empty", nameof(embedRequest.Texts));
        }

        var client = _httpClient;

        var payload = new
        {
            texts = embedRequest.Texts,
            model = embedRequest.Model ?? CohereModels.EmbedEnglishV2,
            input_type = embedRequest.InputType,
            embedding_types = embedRequest.EmbeddingTypes,
            truncate = embedRequest.Truncate ?? "END"
        };

        string jsonPayload = HttpClientPayloadSerializerHelper.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/v1/embed")
        {
            Content = content,
        };
        request.Headers.Add("Authorization", $"bearer {_apiKey}");

        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var responseError = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to send request: {response.StatusCode} - {responseError}");
        }

        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine(responseString);
        return JsonSerializer.Deserialize<EmbedResult>(responseString)!;
    }

}
