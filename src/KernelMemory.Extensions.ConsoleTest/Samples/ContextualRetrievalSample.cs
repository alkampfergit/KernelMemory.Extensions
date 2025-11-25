using KernelMemory.ElasticSearch.Anthropic;
using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.ConsoleTest.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.Image;
using Microsoft.KernelMemory.DataFormats.Office;
using Microsoft.KernelMemory.DataFormats.Pdf;
using Microsoft.KernelMemory.DataFormats.Text;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Spectre.Console;
using KernelMemory.Extensions.DocumentExtraction;

namespace SemanticMemory.Samples;

public class ContextualRetrievalSample : ISample
{
    public async Task RunSample(string bookPdf)
    {
        var services = new ServiceCollection();
        services.AddLogging(l => l
            .SetMinimumLevel(LogLevel.Trace)
            .AddConsole()
            .AddDebug()
        );
        //do not forget to add decoders
        //services.AddDefaultContentDecoders();
        //you can add decoder directly in this way
        #pragma warning disable KMEXP00 // 'TextDecoder' is for evaluation purposes only and is subject to change or removal in future updates.
        // services.AddSingleton<IContentDecoder, TextDecoder>();
        // services.AddSingleton<IContentDecoder, MarkDownDecoder>();
        // services.AddSingleton<IContentDecoder, HtmlDecoder>();

        // //services.AddSingleton<IContentDecoder, PdfDecoder>();
        // //services.AddSingleton<IContentDecoder, PdfStructuredDocumentDecoder>();

        // services.AddSingleton<IContentDecoder, ImageDecoder>();
        // services.AddSingleton<IContentDecoder, MsExcelDecoder>();
        // services.AddSingleton<IContentDecoder, MsPowerPointDecoder>();
        // services.AddSingleton<IContentDecoder, MsWordDecoder>();
        #pragma warning restore KMEXP00

        services.AddHttpClient<RawAnthropicHttpClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Configure standard resilience options here
            });
        services.AddTransient<RawAnthropicClient>();

        var anthropicApiKey = Dotenv.Get("ANTHROPIC_API_KEY")!;
        if (string.IsNullOrEmpty(anthropicApiKey))
        {
            throw new Exception("ANTHROPIC_API_KEY is not set");
        }

        var config = new AnthropicTextGenerationConfiguration()
        {
            ApiKey = anthropicApiKey,
        };
        services.AddSingleton(config);

        var builder = CreateBasicKernelMemoryBuilder(services);
        var kernelMemory = builder.Build<MemoryServerless>();

        var orchestrator = builder.GetOrchestrator();

        var serviceProvider = services.BuildServiceProvider();
        var decoders = serviceProvider.GetServices<IContentDecoder>();

        var anthropicClient = serviceProvider.GetRequiredService<RawAnthropicClient>();
        // Add pipeline handlers
        Console.WriteLine("* Defining pipeline handlers...");

        TextExtractionHandler textExtraction = new("extract", orchestrator, decoders);
        await orchestrator.AddHandlerAsync(textExtraction);

        TextCleanerHandler textCleanerHandler = new("clean", orchestrator);
        await orchestrator.AddHandlerAsync(textCleanerHandler);

        TextPartitioningHandler textPartitioning = new("partition", orchestrator);
        await orchestrator.AddHandlerAsync(textPartitioning);

        ClaudeContextualRetrievalHandler contextual = new("contextual", orchestrator, anthropicClient);
        await orchestrator.AddHandlerAsync(contextual);

        GenerateEmbeddingsHandler textEmbedding = new("gen_embeddings", orchestrator);
        await orchestrator.AddHandlerAsync(textEmbedding);

        SaveRecordsHandler saveRecords = new("save_records", orchestrator);
        await orchestrator.AddHandlerAsync(saveRecords);

        // orchestrator.AddHandlerAsync(...);
        // orchestrator.AddHandlerAsync(...);

        // Create sample pipeline with 4 files
        var index = AnsiConsole.Confirm("Do you want to index document?");
        if (index)
        {
            Console.WriteLine("Single pipeline for single document");
            var pipeline = orchestrator
                .PrepareNewDocumentUpload(
                    index: "book",
                    documentId: "grayhatpython",
                    new TagCollection { { "security", "books" } })
                .AddUploadFile("file1", Path.GetFileName(bookPdf), bookPdf)
                .Then("extract")
                .Then("clean")
                .Then("partition")
                .Then("contextual")
                .Then("gen_embeddings")
                .Then("save_records")
                .Build();

            // Execute pipeline
            Console.WriteLine("* Executing pipeline...");
            await orchestrator.RunPipelineAsync(pipeline);
        }
        string question;
        do
        {
            Console.WriteLine("Ask a question to the kernel memory:");
            question = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(question))
            {
                var response = await kernelMemory.AskAsync(question, index: "book");
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

        kernelMemoryBuilder.WithContentDecoder<PdfStructuredDocumentDecoder>();

        kernelMemoryBuilder
            .WithSimpleFileStorage(new SimpleFileStorageConfig()
            {
                //Directory = "/tmp/km/storage",
                Directory = "c:\\temp\\km2\\storage",
                StorageType = FileSystemTypes.Disk
            })
            .WithSimpleVectorDb(new SimpleVectorDbConfig()
            {
                //Directory = "/tmp/km/vectorstorage",
                Directory = "c:\\temp\\km2\\vectorstorage",
                StorageType = FileSystemTypes.Disk
            });

        services.AddSingleton<IKernelMemoryBuilder>(kernelMemoryBuilder);
        return kernelMemoryBuilder;
    }
}
