using KernelMemory.Extensions.ConsoleTest.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

namespace SemanticMemory.Samples;

public class SimpleBookIndexingWithTextCleaning : ISample
{
    public async Task RunSample(string bookPdf)
    {
        var services = new ServiceCollection();

        //do not forget to add decoders
        services.AddDefaultContentDecoders();

        var builder = CreateBasicKernelMemoryBuilder(services);
        var kernelMemory = builder.Build<MemoryServerless>();

        var orchestrator = builder.GetOrchestrator();

        var serviceProvider = services.BuildServiceProvider();
        var decoders = serviceProvider.GetServices<IContentDecoder>();

        // Add pipeline handlers
        Console.WriteLine("* Defining pipeline handlers...");

        TextExtractionHandler textExtraction = new("extract", orchestrator, decoders);
        await orchestrator.AddHandlerAsync(textExtraction);

        TextCleanerHandler textCleanerHandler = new("clean", orchestrator);
        await orchestrator.AddHandlerAsync(textCleanerHandler);

        TextPartitioningHandler textPartitioning = new("partition", orchestrator);
        await orchestrator.AddHandlerAsync(textPartitioning);

        SummarizationHandler summarizeEmbedding = new("summarize", orchestrator);
        await orchestrator.AddHandlerAsync(summarizeEmbedding);

        GenerateEmbeddingsHandler textEmbedding = new("gen_embeddings", orchestrator);
        await orchestrator.AddHandlerAsync(textEmbedding);

        SaveRecordsHandler saveRecords = new("save_records", orchestrator);
        await orchestrator.AddHandlerAsync(saveRecords);

        // orchestrator.AddHandlerAsync(...);
        // orchestrator.AddHandlerAsync(...);

        // Create sample pipeline with 4 files
        Console.WriteLine("* Defining pipeline with 4 files...");
        var pipeline = orchestrator
            .PrepareNewDocumentUpload(
                index: "booksample",
                documentId: "booksample",
                new TagCollection { { "example", "books" } })
            .AddUploadFile("file1", Path.GetFileName(bookPdf), bookPdf)
            .Then("extract")
            .Then("clean")
            .Then("partition")
            .Then("summarize")
            .Then("gen_embeddings")
            .Then("save_records")
            .Build();

        // Execute pipeline
        Console.WriteLine("* Executing pipeline...");
        await orchestrator.RunPipelineAsync(pipeline);
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
                Directory = "c:\\temp\\km\\storage",
                StorageType = FileSystemTypes.Disk
            })
            .WithSimpleVectorDb(new SimpleVectorDbConfig()
            {
                Directory = "c:\\temp\\km\\vectorstorage",
                StorageType = FileSystemTypes.Disk
            });

        services.AddSingleton<IKernelMemoryBuilder>(kernelMemoryBuilder);
        return kernelMemoryBuilder;
    }
}
