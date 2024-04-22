namespace KernelMemory.ElasticSearch.Anthropic
{
    public class AnthropicTextGenerationConfiguration
    {
        /// <summary>
        /// This allows configuring the client that will be used for httpclient factory to create client.
        /// </summary>
        public string? HttpClientName { get; set; }

        public int MaxTokenTotal { get; set; } = 4096;

        public string ApiKey { get; set; }

        public string ModelName { get; set; } = HaikuModelName;

        public const string HaikuModelName = "claude-3-haiku-20240307";
        public const string SonnetModelName = "claude-3-sonnet-20240229";
        public const string OpusModelName = "claude-3-opus-20240229";
    }
}
