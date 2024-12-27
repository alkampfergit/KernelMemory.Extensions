using KernelMemory.Extensions.ConsoleTest.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

namespace SemanticMemory.Samples;

internal class CustomParsersSample : ISample
{
    public async Task RunSample(string fileToParse)
    {
        var services = new ServiceCollection();

        services.AddLogging(l => l
            .SetMinimumLevel(LogLevel.Trace)
            .AddConsole()
            .AddDebug()
        );

        var builder = CreateBasicKernelMemoryBuilder(services);

        var serviceProvider = services.BuildServiceProvider();
        var parserClient = serviceProvider.GetRequiredService<LLamaCloudParserClient>();

        //This is not so goot, but it seems that when we build the ServerlessMemory object
        //it cannot access the http services registered in the service collection
        builder.Services.AddSingleton(parserClient);

        var kernelMemory = builder.Build<MemoryServerless>();

        var orchestrator = builder.GetOrchestrator();

        var decoders = serviceProvider.GetServices<IContentDecoder>();

        // Add pipeline handlers
        Console.WriteLine("* Defining pipeline handlers...");

        TextExtractionHandler textExtraction = new("extract", orchestrator, decoders);
        await orchestrator.AddHandlerAsync(textExtraction);

        TextPartitioningHandler textPartitioning = new("partition", orchestrator);
        await orchestrator.AddHandlerAsync(textPartitioning);

        GenerateEmbeddingsHandler textEmbedding = new("gen_embeddings", orchestrator);
        await orchestrator.AddHandlerAsync(textEmbedding);

        SaveRecordsHandler saveRecords = new("save_records", orchestrator);
        await orchestrator.AddHandlerAsync(saveRecords);

        var fileName = Path.GetFileName(fileToParse);

        var contextProvider = serviceProvider.GetRequiredService<IContextProvider>();

        // now we are going to index document, llamacloud can use caching so we can avoid asking for file.
        var pipelineBuilder = orchestrator
            .PrepareNewDocumentUpload(
                index: "llamacloud",
                documentId: fileName,
                new TagCollection { { "example", "books" } })
            .AddUploadFile(fileName, fileName, fileToParse)
            .Then("extract")
            .Then("partition")
            .Then("gen_embeddings")
            .Then("save_records");

        contextProvider.AddLLamaCloudParserOptions(fileName, "This is a manual for Dreame vacuum cleaner, I need you to extract a series of sections that can be useful for an helpdesk to answer user questions. You will create sections where each sections contains a question and an answer taken from the text.");

        var pipeline = pipelineBuilder.Build();
        await orchestrator.RunPipelineAsync(pipeline);

        // now ask a question to the user continuously until the user ask an empty question
        string question;
        do
        {
            Console.WriteLine("Ask a question to the kernel memory:");
            question = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(question))
            {
                var response = await kernelMemory.AskAsync(question);
                Console.WriteLine(response.Result);
            }
        } while (!string.IsNullOrWhiteSpace(question));
    }

    private static IKernelMemoryBuilder CreateBasicKernelMemoryBuilder(
        ServiceCollection services)
    {
        // we need a series of services to use Kernel Memory, the first one is
        // an embedding service that will be used to create dense vector for
        // pieces of test. We can use standard ADA embedding service
        var embeddingConfig = new AzureOpenAIConfig
        {
            APIKey = Dotenv.Get("OPENAI_API_KEY"),
            Deployment = "text-embedding-ada-002",
            Endpoint = Dotenv.Get("AZURE_ENDPOINT"),
            APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey
        };

        // Now kenel memory needs the LLM data to be able to pass question
        // and retreived segments to the model. We can Use GPT35
        var chatConfig = new AzureOpenAIConfig
        {
            APIKey = Dotenv.Get("OPENAI_API_KEY"),
            Deployment = Dotenv.Get("KERNEL_MEMORY_DEPLOYMENT_NAME"),
            Endpoint = Dotenv.Get("AZURE_ENDPOINT"),
            APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            MaxTokenTotal = 4096
        };

        var kernelMemoryBuilder = new KernelMemoryBuilder(services)
            .WithAzureOpenAITextGeneration(chatConfig)
            .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig);

        kernelMemoryBuilder
           .WithSimpleFileStorage(new SimpleFileStorageConfig()
           {
               Directory = "c:\\temp\\kmcps\\storage",
               StorageType = FileSystemTypes.Disk
           })
           .WithSimpleVectorDb(new SimpleVectorDbConfig()
           {
               Directory = "c:\\temp\\kmcps\\vectorstorage",
               StorageType = FileSystemTypes.Disk
           });

        kernelMemoryBuilder.WithContentDecoder<LLamaCloudParserDocumentDecoder>();

        var llamaApiKey = Environment.GetEnvironmentVariable("LLAMA_API_KEY");
        if (string.IsNullOrEmpty(llamaApiKey))
        {
            throw new Exception("LLAMA_API_KEY is not set");
        }

        //Create llamaparser client
        services.AddSingleton(new CloudParserConfiguration
        {
            ApiKey = llamaApiKey,
        });

        services.AddHttpClient<LLamaCloudParserClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Configure standard resilience options here
                options.TotalRequestTimeout = new HttpTimeoutStrategyOptions()
                {
                    Timeout = TimeSpan.FromMinutes(10),
                };
            });

        services.AddSingleton(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<LLamaCloudParserClient>());

        services.AddSingleton<IKernelMemoryBuilder>(kernelMemoryBuilder);
        return kernelMemoryBuilder;
    }
}
