using KernelMemory.Extensions.Helper;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.ElasticSearch.Anthropic;

public class RawAnthropicClient 
{
    private readonly IServiceProvider _serviceProvider;

    public RawAnthropicClient(
        IServiceProvider serviceProvider
    )
    {
        this._serviceProvider = serviceProvider;
    }

    public async IAsyncEnumerable<StreamingResponseMessage> CallClaudeStreaming(
        CallClaudeStreamingParams parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        await foreach (var message in client.CallClaudeStreaming(parameters, cancellationToken))
        {
            yield return message;
        }
    }

    public Task<MessageResponse> CallClaudeAsync(
        CallClaudeStreamingParams parameters,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        return client.CallClaude(parameters, cancellationToken);
    }

    private RawAnthropicHttpClient CreateClient()
    {
        return _serviceProvider.GetRequiredService<RawAnthropicHttpClient>();
    }
}

public class RawAnthropicHttpClient
{
    private readonly AnthropicTextGenerationConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://api.anthropic.com";

    public RawAnthropicHttpClient(
        AnthropicTextGenerationConfiguration configuration,
        HttpClient httpClient)
    {
        this._configuration = configuration;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Simply invoke the Claude Chat API to get a response with streaming. 
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async IAsyncEnumerable<StreamingResponseMessage> CallClaudeStreaming(
        CallClaudeStreamingParams parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestPayload = new MessageRequest
        {
            Model = parameters.ModelName,
            MaxTokens = parameters.MaxTokens,
            Temperature = parameters.Temperature,
            System = parameters.System,
            Stream = true,
            Messages = parameters.Messages,
        };

        string jsonPayload = HttpClientPayloadSerializerHelper.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Headers.Add("x-api-key", _configuration.ApiKey);
        content.Headers.Add("anthropic-version", "2023-06-01");
        content.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = content,
        };

        var httpClient = _httpClient;
        var response = await httpClient.SendAsync(request, cancellationToken);
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
                string? line = await reader.ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    //this is strange and should not happen, but if we read a null line, we simply need to skip
                    continue;
                }

                //this is the first line of message
                var eventMessage = line.Split(":")[1].Trim();

                //now read the message
                line = await reader.ReadLineAsync(cancellationToken)!;

                if (line == null)
                {
                    //this is strange and should not happen, but if we read a null line, we simply need to skip
                    continue;
                }

                if (eventMessage == "content_block_delta")
                {
                    var data = line.Substring("data: ".Length).Trim();
                    var messageDelta = JsonSerializer.Deserialize<ContentBlockDelta>(data)!;
                    yield return messageDelta;
                }
                else if (eventMessage == "message_stop")
                {
                    break;
                }

                //read the next empty line
                await reader.ReadLineAsync(cancellationToken);
            }
        }
    }

    public async Task<MessageResponse> CallClaude(
        CallClaudeStreamingParams parameters,
        CancellationToken cancellationToken = default)
    {
        var requestPayload = new MessageRequest
        {
            Model = "claude-3-haiku-20240307",
            MaxTokens = 1000,
            Temperature = 0.3,
            System = parameters.System, // Updated to use IReadOnlyCollection<SystemMessage>
            Messages = parameters.Messages,
        };

        string jsonPayload = HttpClientPayloadSerializerHelper.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Headers.Add("x-api-key", _configuration.ApiKey);
        content.Headers.Add("anthropic-version", "2023-06-01");

        if (parameters.HasCacheControl) 
        {
            content.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = content,
        };

        var httpClient = _httpClient;
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseError = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to send request: {response.StatusCode} - {responseError}");
        }
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<MessageResponse>(jsonResponse)!;
    }
}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


public class CallClaudeStreamingParams
{
    public string ModelName { get; set; }
    public IReadOnlyCollection<SystemMessage> System { get; set; } // Updated to IReadOnlyCollection<SystemMessage>
    public IReadOnlyCollection<Message> Messages { get; set; }
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }

    // Adding HasCacheControl property
    public bool HasCacheControl => System?.Any(sm => sm.CacheControl != null) ?? false;
}

public class CacheControl
{
    public static CacheControl Ephemeral {get; private set;}= new CacheControl { Type = "ephemeral" }; 

    [JsonPropertyName("type")]
    public string Type { get; private set; } = "ephemeral";
}

public class MessageRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("system")]
    public IReadOnlyCollection<SystemMessage> System { get; set; }

    [JsonPropertyName("messages")]
    public IReadOnlyCollection<Message> Messages { get; set; }
}

public class Message
{
    public static Message Create(string role, string content)
    {
        return new Message
        {
            Role = role,
            Content = content
        };
    }

    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}

public class SystemMessage
{
    public static SystemMessage Create(string text, CacheControl? cacheControl = null)
    {
        return new SystemMessage
        {
            Type = "text",
            Text = text,
            CacheControl = cacheControl
        };
    }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("cache_control")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CacheControl? CacheControl { get; set; }
}
public class MessageResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("content")]
    public ContentResponse[] Content { get; set; }

    [JsonPropertyName("stop_reason")]
    public string StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; }
}

public class Usage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int CacheCreationInputTokens { get; set; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int CacheReadInputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}


public class ContentResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public abstract class StreamingResponseMessage { }

public class ContentBlockDelta : StreamingResponseMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public Delta Delta { get; set; }
}

public class Delta
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
