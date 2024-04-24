using KernelMemory.ElasticSearch;
using KernelMemory.Extensions;
using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.ConsoleTest.Helper;
using KernelMemory.Extensions.QueryPipeline;
using KernelMemory.Extensions.QueryPipeline.Diagnostic;
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

internal class CustomSearchPipelineBase : ISample2
{
    public async Task RunSample2()
    {
        var services = new ServiceCollection();

        CohereConfiguration cohereConfiguration = new CohereConfiguration();
        cohereConfiguration.ApiKey = Dotenv.Get("COHERE_API_KEY");

        services.AddSingleton(cohereConfiguration);
        services.AddSingleton<RawCohereClient>();
        services.AddHttpClient();

        var storageToUse = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select the storage to use")
            .AddChoices([
                "elasticsearch", "FileSystem (debug)"
        ]));

        var builder = CreateBasicKernelMemoryBuilder(services, storageToUse == "elasticsearch");
        var kernelMemory = builder.Build<MemoryServerless>();

        var serviceProvider = services.BuildServiceProvider();

        //you do not need to index a document each time you start the software
        await ManageIndexingOfDocuments(kernelMemory);

        var vectorDb = serviceProvider.GetRequiredService<IMemoryDb>();
        var storage = serviceProvider.GetRequiredService<IContentStorage>();

        var textGenerator = serviceProvider.GetRequiredService<ITextGenerator>();
        var searchClientConfig = serviceProvider.GetService<SearchClientConfig>();
        var promptProvider = serviceProvider.GetService<IPromptProvider>();

        var questionPipelineFactory = serviceProvider.GetRequiredService<IUserQuestionPipelineFactory>();
        var questionPipeline = questionPipelineFactory.Create();

        // now ask a question to the user continuously until the user ask an empty question
        string? question;
        do
        {
            question = AnsiConsole.Ask<string>("Ask a question to the kernel memory:");
            if (!string.IsNullOrWhiteSpace(question))
            {
                var keywordValidation = AnsiConsole.Ask<string>("Specify string to search in text for validation, empty to avoid validation:");
                var options = new UserQueryOptions("default");
                UserQuestion userQuestion = new UserQuestion(options, question);
                var questionEnumerator = questionPipeline.ExecuteQueryAsync(userQuestion);

                Console.WriteLine("\nAnswerStream:\n");
                await foreach (var step in questionEnumerator)
                {
                    if (step.Type == UserQuestionProgressType.AnswerPart)
                    {
                        Console.Write(step.Text);
                    }
                }

                Console.WriteLine("\n\n");

                //ok we can validate the answer if requested
                if (!string.IsNullOrWhiteSpace(keywordValidation))
                {
                    var probe = new QueryPipelineProbe(QueryPipelineProbeHelper.ForStringContains(keywordValidation));
                    var stats = await probe.AnalyzePipelineAsync(userQuestion);
                    Console.WriteLine("Validation statitsics --------------------------:");
                    foreach (var stat in stats.RetrieveStats)
                    {
                        Console.WriteLine($"For {stat.Key} we have {stat.Value.ValidRecords} valid records out of {stat.Value.TotalRecords}. List of good record is : {string.Join(',', stat.Value.ValidRecordsIndices)}");
                    }

                    Console.WriteLine("After reranking. ");
                    Console.WriteLine($"We have {stats.AfterReranking.ValidRecords} valid records out of {stats.AfterReranking.TotalRecords}. List of good record is : {string.Join(',', stats.AfterReranking.ValidRecordsIndices)}");
                }

                if (!userQuestion.Answered)
                {
                    Console.WriteLine("Answer cannot be retrieved.");
                }
            }
        } while (!string.IsNullOrWhiteSpace(question));
    }

    private static async Task ManageIndexingOfDocuments(MemoryServerless kernelMemory)
    {
        var indexDocument = AnsiConsole.Confirm("Do you want to index documents? (y/n)", true);
        if (indexDocument)
        {
            var singleDocumentIdex = AnsiConsole.Confirm("Do you want to index a single document? (y/n)", true);
            if (singleDocumentIdex)
            {
                //ask for the document to index
                var bookPdf = AnsiConsole.Ask<string>("Enter the path to the document to index:").Trim('\"');
                var docId = Path.GetFileName(bookPdf);
                //Delete any previously indexed document with the same index
                await kernelMemory.DeleteDocumentAsync(docId);
                await IndexDocument(kernelMemory, bookPdf, docId);
            }
            else
            {
                var directoryToIndex = AnsiConsole.Ask<string>("Enter the path to the directory to index. all pdf will be indexed:").Trim('\"');
                var dinfo = new DirectoryInfo(directoryToIndex);
                var files = dinfo.GetFiles("*.pdf", new EnumerationOptions() { RecurseSubdirectories = true });
                foreach (var file in files)
                {
                    AnsiConsole.WriteLine("Indexing document:[blue]" + file.FullName + "[/]");
                    var docId = Path.GetFileNameWithoutExtension(file.Name);
                    //Delete any previously indexed document with the same index
                    await kernelMemory.DeleteDocumentAsync(docId);
                    await IndexDocument(kernelMemory, file.FullName, docId);
                    AnsiConsole.WriteLine("Indexing finished for document:[blue]" + file.FullName + "[/]");
                }
            }
        }
    }

    private static async Task IndexDocument(MemoryServerless kernelMemory, string doc, string docId)
    {
        await kernelMemory.ImportDocumentAsync(doc, docId);

        //while (importDocumentTask.IsCompleted == false)
        //{
        //    var docStatus = await kernelMemory.GetDocumentStatusAsync(docId);
        //    if (docStatus != null)
        //    {
        //        Console.WriteLine("Completed Steps:" + string.Join(",", docStatus.CompletedSteps));
        //    }

        //    await Task.Delay(1000);
        //}
    }

    private static IKernelMemoryBuilder CreateBasicKernelMemoryBuilder(
        ServiceCollection services,
        bool useElasticSearch)
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
           });

        if (useElasticSearch)
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
        services.AddSingleton<CohereReRanker>();
        services.AddSingleton<StandardVectorSearchQueryHandler>();
        services.AddSingleton<KeywordSearchQueryHandler>();
        services.AddSingleton<StandardRagQueryExecutor>();

        //now create the pipeline
        services.AddKernelMemoryUserQuestionPipeline(config =>
        {
            config.AddHandler<StandardVectorSearchQueryHandler>();
            if (useElasticSearch)
            {
                //I can use keyword search
                config.AddHandler<KeywordSearchQueryHandler>();
            }

            config
                .AddHandler<StandardRagQueryExecutor>()
                .SetReRanker<CohereReRanker>();
        });
        return kernelMemoryBuilder;
    }
}
