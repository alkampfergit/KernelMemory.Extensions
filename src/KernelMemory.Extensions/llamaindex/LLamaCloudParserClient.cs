using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

/// <summary>
/// As for https://docs.cloud.llamaindex.ai/API/upload-file-api-v-1-parsing-upload-post
/// </summary>
public class LLamaCloudParserClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LLamaCloudParserClient> _log;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public LLamaCloudParserClient(
        CloudParserConfiguration config,
        HttpClient httpClient,
        ILogger<LLamaCloudParserClient>? log = null)
    {
        if (String.IsNullOrEmpty(config.ApiKey))
        {
            throw new ArgumentException("ApiKey is required", nameof(config.ApiKey));
        }

        _httpClient = httpClient;
    
        _log = log ?? DefaultLogger<LLamaCloudParserClient>.Instance;
        _apiKey = config.ApiKey;
        _baseUrl = config.BaseUrl!;
    }

    public async Task<UploadResponse> UploadAsync(
        Stream fileContent, 
        string fileName,
        UploadParameters? parameters = null)
    {
        var requestUri = $"{_baseUrl.TrimEnd('/')}/api/v1/parsing/upload";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var multipartContent = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileContent);
        multipartContent.Add(streamContent, "file", fileName);

       if (parameters != null && parameters.CustomStringParameters.Count > 0)
        {
            foreach (var (key, value) in parameters.CustomStringParameters)
            {
                multipartContent.Add(new StringContent(value), key);
            }
        }

        request.Content = multipartContent;

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UploadResponse>(jsonResponse) 
            ?? throw new InvalidOperationException("Failed to parse response");
    }

    public async Task<JobResponse> GetJobAsync(string jobId)
    {
        var requestUri = $"{_baseUrl.TrimEnd('/')}/api/v1/parsing/job/{jobId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JobResponse>(jsonResponse) 
            ?? throw new InvalidOperationException("Failed to parse response");
    }

    public async Task<string> GetJobRawMarkdownAsync(string jobId)
    {
        var requestUri = $"{_baseUrl.TrimEnd('/')}/api/v1/parsing/job/{jobId}/result/raw/markdown";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> WaitForJobSuccessAsync(string jobId, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            var jobResponse = await GetJobAsync(jobId);
            if (jobResponse.Status == UploadStatus.SUCCESS)
            {
                return true;
            }
            await Task.Delay(TimeSpan.FromSeconds(10)); // Wait for 10 seconds before retrying
        }
        return false;
    }
}

public class CloudParserConfiguration
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; } = "https://api.cloud.llamaindex.ai";
}

/// <summary>
/// Simplify passing parameters.
/// </summary>
public class UploadParameters
{
    public Dictionary<string, string> CustomStringParameters { get; set; } = new ();
   
    public UploadParameters WithParsingInstructions(string parsingInstructions) 
    {
        CustomStringParameters["parsing_instruction"] = parsingInstructions;
        return this;
    }
}

public class UploadResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadStatus Status { get; set; }

    [JsonPropertyName("error_code")]
    public object? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public object? ErrorMessage { get; set; }
}

public class JobResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadStatus Status { get; set; }

    [JsonPropertyName("error_code")]
    public object? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public object? ErrorMessage { get; set; }
}

public enum UploadStatus
{
    PENDING,
    SUCCESS,
    ERROR,
    PARTIAL_SUCCESS
}