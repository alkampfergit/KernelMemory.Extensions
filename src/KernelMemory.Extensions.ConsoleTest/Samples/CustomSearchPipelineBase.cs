using KernelMemory.ElasticSearch;
using KernelMemory.Extensions;
using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.ConsoleTest.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.ContentStorage;
using Microsoft.KernelMemory.ContentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.KernelMemory.Prompts;
using Spectre.Console;

namespace SemanticMemory.Samples;

internal class CustomSearchPipelineBase : ISample
{
    public async Task RunSample(string bookPdf)
    {
        var services = new ServiceCollection();

        CohereConfiguration cohereConfiguration = new CohereConfiguration();
        cohereConfiguration.ApiKey = Dotenv.Get("COHERE_API_KEY");

        services.AddSingleton(cohereConfiguration);
        services.AddSingleton<RawCohereClient>();
        services.AddHttpClient();

        var builder = CreateBasicKernelMemoryBuilder(services);
        var kernelMemory = builder.Build<MemoryServerless>();

        var serviceProvider = services.BuildServiceProvider();
        var docId = Path.GetFileName(bookPdf);

        //you do not need to index a document each time you start the software
        var indexDocument = AnsiConsole.Confirm("Do you want to index a document? (y/n)", true);
        if (indexDocument)
        {
            await IndexDocument(kernelMemory, bookPdf, docId);
        }

        var vectorDb = serviceProvider.GetRequiredService<IMemoryDb>();
        var storage = serviceProvider.GetRequiredService<IContentStorage>();

        var textGenerator = serviceProvider.GetRequiredService<ITextGenerator>();
        var searchClientConfig = serviceProvider.GetService<SearchClientConfig>();
        var promptProvider = serviceProvider.GetService<IPromptProvider>();

        var questionPipeline = new UserQuestionPipeline();
        questionPipeline.AddHandler(new StandardVectorSearchQueryHandler(vectorDb));

        var advancedDb = serviceProvider.GetService<IMemoryDb>() as IAdvancedMemoryDb;
        if (advancedDb != null)
        {
            questionPipeline.AddHandler(new KeywordSearchQueryHandler(advancedDb));
        }
        questionPipeline.AddHandler(new StandardRagQueryExecutor(textGenerator, searchClientConfig, promptProvider));

        var cohereClient = serviceProvider.GetRequiredService<RawCohereClient>();
        questionPipeline.SetReRanker(new CohereReRanker(cohereClient));

        // now ask a question to the user continuously until the user ask an empty question
        string? question;
        do
        {
            Console.WriteLine("Ask a question to the kernel memory:");
            question = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(question))
            {
                var options = new UserQueryOptions("default");
                UserQuestion userQuestion = new UserQuestion(options, question);
                var questionEnumerator = questionPipeline.ExecuteQueryAsync(userQuestion);

                await foreach (var step in questionEnumerator)
                {
                    if (step.Type == UserQuestionProgressType.AnswerPart)
                    {
                        Console.Write(step.Text);
                    }
                }

                if (!userQuestion.Answered)
                {
                    Console.WriteLine("Answer cannot be retrieved.");
                }
            }
        } while (!string.IsNullOrWhiteSpace(question));
    }

    private static async Task IndexDocument(MemoryServerless kernelMemory, string doc, string docId)
    {
        var importDocumentTask = kernelMemory.ImportDocumentAsync(doc, docId);

        while (importDocumentTask.IsCompleted == false)
        {
            var docStatus = await kernelMemory.GetDocumentStatusAsync(docId);
            if (docStatus != null)
            {
                Console.WriteLine("Completed Steps:" + string.Join(",", docStatus.CompletedSteps));
            }

            await Task.Delay(1000);
        }
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

        var storage = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select the storage to use")
            .AddChoices([
                "elasticsearch", "FileSystem (debug)"
        ]));

        kernelMemoryBuilder
           .WithSimpleFileStorage(new SimpleFileStorageConfig()
           {
               Directory = "c:\\temp\\km\\storage",
               StorageType = FileSystemTypes.Disk
           });

        if (storage == "elasticsearch")
        {
            KernelMemoryElasticSearchConfig kernelMemoryElasticSearchConfig = new KernelMemoryElasticSearchConfig();
            kernelMemoryElasticSearchConfig.ServerAddress = "http://localhost:9800";
            kernelMemoryElasticSearchConfig.IndexPrefix = "km";
            kernelMemoryElasticSearchConfig.ReplicaCount = 1;
            kernelMemoryElasticSearchConfig.ShardNumber = 1;
            kernelMemoryElasticSearchConfig.IndexablePayloadProperties = ["text"];

            kernelMemoryBuilder.WithElasticSearch(kernelMemoryElasticSearchConfig);
        }
        else
        {
            kernelMemoryBuilder.WithSimpleVectorDb(new SimpleVectorDbConfig()
            {
                Directory = "c:\\temp\\km\\vectorstorage",
                StorageType = FileSystemTypes.Disk
            });
        }

        // kernelMemoryBuilder.Services.ConfigureHttpClientDefaults(c => c
        //     .AddLogger(s => _loggingProvider.CreateHttpRequestBodyLogger(s.GetRequiredService<ILogger<DumpLoggingProvider>>())));

        services.AddSingleton<IKernelMemoryBuilder>(kernelMemoryBuilder);
        return kernelMemoryBuilder;
    }
}
