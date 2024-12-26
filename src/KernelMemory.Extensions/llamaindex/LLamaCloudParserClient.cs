using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        this._httpClient = httpClient;
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

        if (parameters != null)
        {
            foreach (var prop in typeof(UploadParameters).GetProperties())
            {
                var value = prop.GetValue(parameters);
                if (value != null)
                {
                    if (value is bool boolValue)
                    {
                        multipartContent.Add(new StringContent(boolValue.ToString().ToLower()), prop.Name);
                    }
                    else if (value is string[] arrayValue)
                    {
                        multipartContent.Add(new StringContent(string.Join(",", arrayValue)), prop.Name);
                    }
                    else
                    {
                        multipartContent.Add(new StringContent(value.ToString()!), prop.Name);
                    }
                }
            }
        }

        request.Content = multipartContent;

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UploadResponse>(jsonResponse) 
            ?? throw new InvalidOperationException("Failed to parse response");
    }
}

public class CloudParserConfiguration
{
    public string? ApiKey { get; internal set; }
    public string? BaseUrl { get; internal set; }
}

public class UploadParameters
{
    public string? ProjectId { get; set; }
    public string? OrganizationId { get; set; }
    public bool AnnotateLinks { get; set; }
    public bool AutoMode { get; set; }
    public bool AutoModeTriggerOnImageInPage { get; set; }
    public bool AutoModeTriggerOnTableInPage { get; set; }
    public string? AutoModeTriggerOnTextInPage { get; set; }
    public string? AutoModeTriggerOnRegexpInPage { get; set; }
    public string? AzureOpenAiApiVersion { get; set; }
    public string? AzureOpenAiDeploymentName { get; set; }
    public string? AzureOpenAiEndpoint { get; set; }
    public string? AzureOpenAiKey { get; set; }
    public float? BboxBottom { get; set; }
    public float? BboxLeft { get; set; }
    public float? BboxRight { get; set; }
    public float? BboxTop { get; set; }
    public bool ContinuousMode { get; set; }
    public bool DisableOcr { get; set; }
    public bool DisableReconstruction { get; set; }
    public bool DisableImageExtraction { get; set; }
    public bool DoNotCache { get; set; }
    public bool DoNotUnrollColumns { get; set; }
    public bool ExtractCharts { get; set; }
    public bool FastMode { get; set; }
    public bool GuessXlsxSheetName { get; set; }
    public bool HtmlMakeAllElementsVisible { get; set; }
    public bool HtmlRemoveFixedElements { get; set; }
    public bool HtmlRemoveNavigationElements { get; set; }
    public string? HttpProxy { get; set; }
    public string? InputS3Path { get; set; }
    public string? InputUrl { get; set; }
    public bool InvalidateCache { get; set; }
    public bool IsFormattingInstruction { get; set; } = true;
    public string[]? Language { get; set; } = new[] { "en" };
    public bool ExtractLayout { get; set; }
    public object? MaxPages { get; set; }
    public bool OutputPdfOfDocument { get; set; }
    public string? OutputS3PathPrefix { get; set; }
    public string? PagePrefix { get; set; }
    public string? PageSeparator { get; set; }
    public string? PageSuffix { get; set; }
    public string? ParsingInstruction { get; set; }
    public bool PremiumMode { get; set; }
    public bool SkipDiagonalText { get; set; }
    public bool StructuredOutput { get; set; }
    public string? StructuredOutputJsonSchema { get; set; }
    public string? StructuredOutputJsonSchemaName { get; set; }
    public bool TakeScreenshot { get; set; }
    public string? TargetPages { get; set; }
    public bool UseVendorMultimodalModel { get; set; }
    public string? VendorMultimodalApiKey { get; set; }
    public string? VendorMultimodalModelName { get; set; }
    public string? WebhookUrl { get; set; }
    public string? BoundingBox { get; set; }
    public bool Gpt4OMode { get; set; }
    public string? Gpt4OApiKey { get; set; }
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

public enum UploadStatus
{
    PENDING,
    SUCCESS,
    ERROR,
    PARTIAL_SUCCESS
}