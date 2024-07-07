namespace KernelMemory.ElasticSearch.Anthropic;

public class AnthropicTextGenerationConfiguration
{
    public int MaxTokenTotal { get; set; } = 4096;

    public string ApiKey { get; set; }

    public string ModelName { get; set; } = HaikuModelName;

    public const string HaikuModelName = "claude-3-haiku-20240307";
    public const string SonnetModelName = "claude-3-sonnet-20240229";
    
    public const string Sonnet35ModelName = "claude-3-5-sonnet-20240620";
    public const string OpusModelName = "claude-3-opus-20240229";
}
