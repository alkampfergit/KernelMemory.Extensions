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

public class RawCohereReRankerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RawCohereChatClient> _log;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public RawCohereReRankerClient(
        CohereReRankConfiguration config,
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
    /// https://docs.cohere.com/reference/rerank
    /// </summary>
    public async Task<ReRankResult> ReRankAsync(
        CohereReRankRequest reRankRequest,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClient;
        if (!reRankRequest.Answers.Any()) 
        {
            //Caller does not specify any answer, we have nothing to reorder.
            return ReRankResult.Empty;
        }

        var payload = new CohereReRankRequestBody()
        {
            Model = "rerank-english-v3.0",
            Query = reRankRequest.Question,
            Documents = reRankRequest.Answers,
            TopN = reRankRequest.Answers.Length,
        };
        string jsonPayload = HttpClientPayloadSerializerHelper.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/v1/rerank")
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
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<ReRankResult>(responseString)!;
    }

}
