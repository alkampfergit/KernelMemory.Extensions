using KernelMemory.ElasticSearch.Anthropic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.AI.Anthropic;

namespace KernelMemory.Extensions.FunctionalTests;

public class AnthropicTests
{
    private readonly ServiceProvider _serviceProvider;

    public AnthropicTests()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<RawAnthropicHttpClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Configure standard resilience options here
            });

        var anthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!;
        if (string.IsNullOrEmpty(anthropicApiKey))
        {
            throw new Exception("ANTHROPIC_API_KEY is not set");
        }

        var config = new AnthropicTextGenerationConfiguration() 
        {
            ApiKey = anthropicApiKey,
        };
        services.AddSingleton(config);

        _serviceProvider = services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(AnthropicTextGenerationConfiguration.HaikuModelName)]
    [InlineData(AnthropicTextGenerationConfiguration.Sonnet35ModelName)]
    public async Task Simple_call_some_models(string modelName) 
    {
        var client = new RawAnthropicClient(_serviceProvider);
        var parameters = new CallClaudeStreamingParams() 
        {
            ModelName = modelName,
            System = "You are an helpful assistant!",
            Prompt = "What is the capital of the United States?",
            MaxTokens = 5,
        };

        var result = client.CallClaudeStreaming(parameters);
        var response = await result.ToListAsync();
        Assert.True(response.Count > 0);
    }
}
