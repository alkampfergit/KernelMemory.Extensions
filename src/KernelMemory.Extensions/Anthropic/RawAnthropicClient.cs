using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KernelMemory.ElasticSearch.Anthropic;

public class RawAnthropicClient
{
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _httpClientName;
    private readonly string _baseUrl = "https://api.anthropic.com";

    public RawAnthropicClient(
        string apiKey,
        IHttpClientFactory httpClientFactory,
        string? httpClientName)
    {
        _apiKey = apiKey;
        _httpClientFactory = httpClientFactory;
        _httpClientName = httpClientName;
    }

    public class CallClaudeStreamingParams
    {
        public string ModelName { get; set; }
        public string System { get; set; }
        public string Prompt { get; set; }
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
    }

    public async IAsyncEnumerable<StreamingResponseMessage> CallClaudeStreaming(CallClaudeStreamingParams parameters)
    {
        var requestPayload = new MessageRequest
        {
            Model = parameters.ModelName,
            MaxTokens = parameters.MaxTokens,
            Temperature = parameters.Temperature,
            System = parameters.System,
            Stream = true,
            Messages = new[]
            {
                new Message
                {
                    Role = "user",
                    Content = parameters.Prompt
                }
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Headers.Add("x-api-key", _apiKey);
        content.Headers.Add("anthropic-version", "2023-06-01");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = content,
        };

        var httpClient = GetHttpClient();
        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var responseError = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to send request: {response.StatusCode} - {responseError}");
        }
        response.EnsureSuccessStatusCode();
        var responseStream = await response.Content.ReadAsStreamAsync();
        using (StreamReader reader = new(responseStream))
        {
            while (!reader.EndOfStream)
            {
                string line = await reader.ReadLineAsync();

                //this is the first line of message
                var eventMessage = line.Split(":")[1].Trim();

                //now read the message
                line = await reader.ReadLineAsync()!;

                if (eventMessage == "content_block_delta")
                {
                    var data = line.Substring("data: ".Length).Trim();
                    var messageDelta = JsonSerializer.Deserialize<ContentBlockDelta>(data);
                    yield return messageDelta;
                }
                else if (eventMessage == "message_stop")
                {
                    break;
                }

                //read the next empty line
                await reader.ReadLineAsync();
            }
        }
    }

    private HttpClient GetHttpClient()
    {
        if (string.IsNullOrEmpty(_httpClientName))
        {
            return _httpClientFactory.CreateClient();
        }
        return _httpClientFactory.CreateClient(_httpClientName);
    }

    public async Task<MessageResponse> CallClaude(string prompt)
    {
        var requestPayload = new MessageRequest
        {
            Model = "claude-3-haiku-20240307",
            MaxTokens = 1000,
            Temperature = 0.3,
            System = "You are a nice storyteller",
            Messages = new[]
            {
                new Message
                {
                    Role = "user",
                    Content = prompt
                }
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Headers.Add("x-api-key", _apiKey);
        content.Headers.Add("anthropic-version", "2023-06-01");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = content,
        };

        var httpClient = GetHttpClient();
        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var responseError = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to send request: {response.StatusCode} - {responseError}");
        }
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MessageResponse>(jsonResponse);
    }
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
    public string System { get; set; }

    [JsonPropertyName("messages")]
    public Message[] Messages { get; set; }
}

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}

public class MessageResponse
{
    [JsonPropertyName("content")]
    public ContentResponse[] Content { get; set; }
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
