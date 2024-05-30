using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Cohere;

public class RawCohereClient
{
    private readonly ILogger<RawCohereClient> _log;
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _httpClientName;
    private readonly string _baseUrl;

    public RawCohereClient(
        CohereConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<RawCohereClient>? log = null)
    {
        if (String.IsNullOrEmpty(config.ApiKey))
        {
            throw new ArgumentException("ApiKey is required", nameof(config.ApiKey));
        }
        _log = log ?? DefaultLogger<RawCohereClient>.Instance;
        _apiKey = config.ApiKey;
        _baseUrl = config.BaseUrl;
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
    public async Task<ReRankResult> ReRankAsync(
        CohereReRankRequest reRankRequest,
        CancellationToken cancellationToken = default)
    {
        if (!reRankRequest.Answers.Any()) 
        {
            //Caller does not specify any answer, we have nothing to reorder.
            return ReRankResult.Empty;
        }

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

        var client = CreateHttpClient();

        string jsonPayload = JsonSerializer.Serialize(cohereRagRequest);
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

        var client = CreateHttpClient();
        //force streaming
        cohereRagRequest.Stream = true;

        string jsonPayload = JsonSerializer.Serialize(cohereRagRequest);
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

public async Task<EmbedResult> EmbedAsync(
    CohereEmbedRequest embedRequest,
    CancellationToken cancellationToken = default)
{
    if (embedRequest.Texts == null || !embedRequest.Texts.Any())
    {
        throw new ArgumentException("Texts array cannot be null or empty", nameof(embedRequest.Texts));
    }

    var client = CreateHttpClient();

    var payload = new
    {
        texts = embedRequest.Texts,
        model = embedRequest.Model ?? CohereModels.EmbedEnglishV2,
        input_type = embedRequest.InputType,
        embedding_types = embedRequest.EmbeddingTypes,
        truncate = embedRequest.Truncate ?? "END"
    };

    string jsonPayload = JsonSerializer.Serialize(payload);
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
Console.WriteLine(responseString    );
    return JsonSerializer.Deserialize<EmbedResult>(responseString)!;
}

}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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

public class CohereRagRequest
{
    /// <summary>
    /// Factory method to create a request directly from the user question and memory records
    /// </summary>
    /// <param name="question">Question of the user</param>
    /// <param name="memoryRecords">MemoryRecords you want to pass to answer query.</param>
    /// <returns></returns>
    public static CohereRagRequest CreateFromMemoryRecord(string question, IEnumerable<MemoryRecord> memoryRecords)
    {
        //create a request body directly with system.text.json
        CohereRagRequest ragRequest = new CohereRagRequest()
        {
            Message = question,
            Documents = new List<RagDocument>()
        };

        foreach (var memory in memoryRecords)
        {
            ragRequest.Documents.Add(new RagDocument()
            {
                DocId = memory.Id,
                Text = memory.GetPartitionText()
            });
        }

        return ragRequest;
    }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "command-r-plus";

    [JsonPropertyName("documents")]
    public List<RagDocument> Documents { get; set; }

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; internal set; }

    [JsonPropertyName("preamble")]
    public string Preamble { get; set; }

    [JsonPropertyName("chat_history")]
    public List<ChatMessage> ChatHistory { get; set; }

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; }

    [JsonPropertyName("prompt_truncation")]
    public string PromptTruncation { get; set; } = "OFF";

    [JsonPropertyName("connectors")]
    public List<Connector> Connectors { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("max_input_tokens")]
    public int? MaxInputTokens { get; set; }

    [JsonPropertyName("k")]
    public int? K { get; set; }

    [JsonPropertyName("p")]
    public float? P { get; set; } = 0.75f;

    [JsonPropertyName("seed")]
    public float? Seed { get; set; }

    [JsonPropertyName("stop_sequences")]
    public List<string> StopSequences { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public float? FrequencyPenalty { get; set; } = 0.0f;

    [JsonPropertyName("presence_penalty")]
    public float? PresencePenalty { get; set; } = 0.0f;

    [JsonPropertyName("tools")]
    public List<Tool> Tools { get; set; }

    [JsonPropertyName("tool_results")]
    public List<ToolResult> ToolResults { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class Connector
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("user_access_token")]
    public string UserAccessToken { get; set; }

    [JsonPropertyName("continue_on_failure")]
    public bool ContinueOnFailure { get; set; } = false;

    [JsonPropertyName("options")]
    public Dictionary<string, string> Options { get; set; }

    [JsonPropertyName("search_queries_only")]
    public bool SearchQueriesOnly { get; set; } = false;
}

public class Tool
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("parameter_definitions")]
    public Dictionary<string, ToolParameter> ParameterDefinitions { get; set; }
}

public class ToolParameter
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

public class ToolResult
{
    [JsonPropertyName("call")]
    public ToolCall Call { get; set; }

    [JsonPropertyName("outputs")]
    public List<Dictionary<string, object>> Outputs { get; set; }
}

public class ToolCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; }
}

public class CohereRagResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("citations")]
    public List<Citation> Citations { get; set; }

    [JsonPropertyName("documents")]
    public List<RagDocument> Documents { get; set; }
}

public class Citation
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("document_ids")]
    public List<string> DocumentIds { get; set; }
}

public class RagDocument
{
    [JsonPropertyName("docid")]
    public string DocId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public class ReRankDocument
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
}

public class Result
{
    [JsonPropertyName("document")]
    public ReRankDocument? Document { get; set; }

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
    public static ReRankResult Empty { get; } = new ReRankResult()
    {
        Results = (new List<Result>()).AsReadOnly(),
    };

    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("results")]
    public IReadOnlyList<Result> Results { get; set; } = null!;

    [JsonPropertyName("meta")]
    public Meta Meta { get; set; } = null!;
}

public class ChatStreamEvent
{
    [JsonPropertyName("is_finished")]
    public bool IsFinished { get; set; }

    [JsonPropertyName("event_type")]
    public string EventType { get; set; }

    [JsonPropertyName("generation_id")]
    public string GenerationId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("citations")]
    public List<CohereRagCitation> Citations { get; set; }
}

public class CohereRagCitation
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("document_ids")]
    public List<string> DocumentIds { get; set; }
}
   
public class CohereRagStreamingResponse
{
    public CohereRagResponseType ResponseType { get; set; }

    public string Text { get; set; }

    public List<CohereRagCitation> Citations { get; set; }
}

public enum CohereRagResponseType
{
    Unknown = 0,
    Text = 1,
    Citations = 2,
}

public static class CohereModels
{
    public const string EmbedEnglishV3 = "embed-english-v3.0";
    public const string EmbedMultilingualV3 = "embed-multilingual-v3.0";
    public const string EmbedEnglishLightV3 = "embed-english-light-v3.0";
    public const string EmbedMultilingualLightV3 = "embed-multilingual-light-v3.0";
    public const string EmbedEnglishV2 = "embed-english-v2.0";
    public const string EmbedEnglishLightV2 = "embed-english-light-v2.0";
    public const string EmbedMultilingualV2 = "embed-multilingual-v2.0";
}

public class CohereEmbedRequest
{
    public IEnumerable<string> Texts { get; set; }
    public string Model { get; set; }
    public string InputType { get; set; }
    public IEnumerable<string> EmbeddingTypes { get; set; }
    public string Truncate { get; set; }
}

public static class CohereInputTypes
{
    public const string SearchDocument = "search_document";
    public const string SearchQuery = "search_query";
    public const string Classification = "classification";
    public const string Clustering = "clustering";
}
public class EmbedResult
{
    [JsonPropertyName("responseType")]
    public string ResponseType { get; set; }
    
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("embeddings")]
    public Embeddings Embeddings { get; set; }
    
    [JsonPropertyName("texts")]
    public List<string> Texts { get; set; }
    
    [JsonPropertyName("meta")]
    public Meta Meta { get; set; }
}

public class Embeddings
 {
    [JsonPropertyName("float")]
    public List<float[]> Values { get; set; }
}


#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.