using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Cohere;

public class CohereConfiguration
{
    public string ApiKey { get; set; }

    public string? HttpFactoryClientName { get; set; }
}

public class RawCohereClient
{
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _httpClientName;
    private readonly string _baseUrl = "https://api.cohere.ai/";

    public RawCohereClient(
        CohereConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _apiKey = config.ApiKey;
        _httpClientFactory = httpClientFactory;
        _httpClientName = config.HttpFactoryClientName;
    }

    private HttpClient CreateHttpClient()
    {
        if (string.IsNullOrEmpty(_httpClientName))
        {
            return _httpClientFactory.CreateClient();
        }

        return _httpClientFactory.CreateClient(_httpClientName);
    }

    /// <summary>
    /// https://docs.cohere.com/reference/rerank
    /// </summary>
    /// <param name="reRankRequest"></param>
    /// <returns></returns>
    public async Task<ReRankResult> ReRankAsync(CohereReRankRequest reRankRequest)
    {
        var client = CreateHttpClient();

        var payload = new CohereReRankRequestBody()
        {
            Model = "rerank-english-v3.0",
            Query = reRankRequest.Question,
            Documents = reRankRequest.Answers,
            TopN = reRankRequest.Answers.Length,
        };
        string jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Headers.Add("x-api-key", _apiKey);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/v1/rerank")
        {
            Content = content,
        };
        request.Headers.Add("Authorization", $"bearer {_apiKey}");

        var response = await client.SendAsync(request).ConfigureAwait(false);

        //now perform the request.
        if (!response.IsSuccessStatusCode)
        {
            var responseError = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to send request: {response.StatusCode} - {responseError}");
        }
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<ReRankResult>(responseString);
    }
}

public record CohereReRankRequest(string Question, string[] Answers);

public class CohereReRankRequestBody
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = null!;

    [JsonPropertyName("query")]
    public string Query { get; init; } = null!;

    [JsonPropertyName("top_n")]
    public int TopN { get; init; }

    [JsonPropertyName("documents")]
    public string[] Documents { get; init; } = null!;
}

public class Document
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
}

public class Result
{
    [JsonPropertyName("document")]
    public Document? Document { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; set; }
}

public class ApiVersion
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    [JsonPropertyName("is_deprecated")]
    public bool IsDeprecated { get; set; }

    [JsonPropertyName("is_experimental")]
    public bool IsExperimental { get; set; }
}

public class BilledUnits
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("search_units")]
    public int SearchUnits { get; set; }

    [JsonPropertyName("classifications")]
    public int Classifications { get; set; }
}

public class Tokens
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

public class Meta
{
    [JsonPropertyName("api_version")]
    public ApiVersion ApiVersion { get; set; } = null!;

    [JsonPropertyName("billed_units")]
    public BilledUnits BilledUnits { get; set; } = null!;

    [JsonPropertyName("tokens")]
    public Tokens Tokens { get; set; } = null!;

    [JsonPropertyName("warnings")]
    public string[]? Warnings { get; set; }
}

public class ReRankResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("results")]
    public List<Result> Results { get; set; } = null!;

    [JsonPropertyName("meta")]
    public Meta Meta { get; set; } = null!;
}