using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

namespace KernelMemory.Extensions.FunctionalTests.LLamaCloud;

/// <summary>
/// All these tests are not meant to run in CI because they will use too much
/// free credit so you should uncomment and run to verify if something on the
/// base api changes.
/// </summary>
public class LLamaCloudTests
{
    private readonly ServiceProvider _serviceProvider;

    public LLamaCloudTests()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<LLamaCloudParserClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Configure standard resilience options here
                options.TotalRequestTimeout = new HttpTimeoutStrategyOptions()
                {
                    Timeout = TimeSpan.FromMinutes(10),
                };
            });

        var llamaApiKey = Environment.GetEnvironmentVariable("LLAMA_API_KEY");

        if (string.IsNullOrEmpty(llamaApiKey))
        {
            //llamaApiKey = "if you want to avoid env variables. put-your-api-key-here-do-not-commit";
            throw new Exception("LLAMA_API_KEY is not set");
        }

        services.AddSingleton(new CloudParserConfiguration
        {
            ApiKey = llamaApiKey,
        });

        services.AddSingleton(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<LLamaCloudParserClient>());
    }

    //[Fact]
    public async Task UploadFile_Success()
    {
        var client = _serviceProvider.GetRequiredService<LLamaCloudParserClient>();
        using var fileStream = new FileStream(@"S:\OneDrive\temp\manualeDreame.pdf", FileMode.Open, FileAccess.Read, FileShare.Read);

        var response = await client.UploadAsync(fileStream, "manualeDreame.pdf");

        Assert.NotNull(response);
    }

    //[Fact]
    public async Task UploadFile_with_instructions_Success()
    {
        var client = _serviceProvider.GetRequiredService<LLamaCloudParserClient>();
        using var fileStream = new FileStream(@"S:\OneDrive\temp\manualeDreame.pdf", FileMode.Open, FileAccess.Read, FileShare.Read);

        var parameters = new UploadParameters();
        parameters.WithParsingInstructions(@"This is a manual for Dreame vacuum cleaner, I need you to extract a series of sections that can be useful
for an helpdesk to answer user questions. You will create sections where each sections contains a question and an answer taken from the text.");
        var response = await client.UploadAsync(fileStream, "manualeDreame.pdf", parameters);

        Assert.NotNull(response);
    }

    //[Fact]
    public async Task Wait_for_job_success()
    {
        var client = _serviceProvider.GetRequiredService<LLamaCloudParserClient>();
        var jobId = "1f7dfde3-04eb-49e7-906a-fa9362653905";
        var response = await client.WaitForJobSuccessAsync(jobId, TimeSpan.FromSeconds(20));
        Assert.True(response);
    }

    //[Fact]
    public async Task Get_job_markdown()
    {
        var client = _serviceProvider.GetRequiredService<LLamaCloudParserClient>();
        var jobId = "d566b908-e1ed-4a6a-b632-f4b7280786b2";
        var response = await client.GetJobRawMarkdownAsync(jobId);
        Assert.NotNull(response);
    }
}
