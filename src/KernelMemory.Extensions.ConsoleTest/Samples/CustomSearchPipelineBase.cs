using KernelMemory.ElasticSearch;
using KernelMemory.Extensions;
using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.ConsoleTest.Helper;
using KernelMemory.Extensions.Helper;
using KernelMemory.Extensions.QueryPipeline;
using KernelMemory.Extensions.QueryPipeline.Diagnostic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.KernelMemory.Prompts;
using Microsoft.SemanticKernel;
using Spectre.Console;

namespace SemanticMemory.Samples;

internal class CustomSearchPipelineBase : ISample2
{
    private static DumpLoggingProvider _loggingProvider = new DumpLoggingProvider();

    public async Task RunSample2()
    {
        var services = new ServiceCollection();

        var cohereAzureBaseUrl = Dotenv.Get("COHERE_AZURE_BASE_URL");
        if (string.IsNullOrEmpty(cohereAzureBaseUrl))
        {
            var apiKey = Dotenv.Get("COHERE_API_KEY")!;
            services.ConfigureCohereChat(apiKey);
        }
        else
        {
            var azureApiKey = Dotenv.Get("COHERE_AZURE_API_KEY");
            services.ConfigureCohereChat(azureApiKey, cohereAzureBaseUrl);
        }
        //verify if rerank has a different api key (because the apikey point on azure ai studio)
        var rerankAzureBaseUrl = Dotenv.Get("COHERE_AZURE_RERANK_BASE_URL");
        if (string.IsNullOrEmpty(rerankAzureBaseUrl))
        {
            var apiKey = Dotenv.Get("COHERE_API_KEY")!;
            services.ConfigureCohereRerank(apiKey);
        }
        else
        {
            var azureReRankApiKey = Dotenv.Get("COHERE_AZURE_RERANK_API_KEY");
            services.ConfigureCohereRerank(azureReRankApiKey, rerankAzureBaseUrl);
        }

        services.AddHttpClient<RawCohereChatClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Configure standard resilience options here
            });
        services.AddHttpClient<RawCohereReRankerClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Configure standard resilience options here
            });
        services.AddHttpClient<RawCohereEmbeddingClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Configure standard resilience options here
            });

        CohereCommandRQueryExecutorConfiguration coereCommandRagQueryExecutorConfiguration = new();
        coereCommandRagQueryExecutorConfiguration.MaxMemoryRecord = 10;

        services.AddSingleton(coereCommandRagQueryExecutorConfiguration);
        services.AddSingleton<RawCohereClient>();
        services.AddSingleton<CohereCommandRQueryExecutor>();
        services.AddHttpClient();

        var storageToUse = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select the storage to use")
            .AddChoices([
                "elasticsearch", "FileSystem (debug)"
        ]));

        var queryExecutorToUse = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select the query executor to use")
            .AddChoices([
                "KernelMemory Default",
                "Cohere CommandR+",
                "OpenAI Tool"]));

        var queryRewriterTool = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select query rewriter")
            .AddChoices(["Semantic Kernel Base", "Semantic Kernel Handlebar"]));

        var useHyde = AnsiConsole.Confirm("Do you want to use HyDe? (y/n)", false);

        var kernelBuider = CreateBasicKernelBuilder();
        var builder = CreateBasicKernelMemoryBuilder(
            services,
            storageToUse == "elasticsearch",
            queryExecutorToUse,
            queryRewriterTool == "Semantic Kernel Handlebar",
            useHyde);
        var kernelMemory = builder.Build<MemoryServerless>();
        var kernel = kernelBuider.Build();

        //Add semantic kernel in DI
        services.AddSingleton<ISemanticKernelWrapper, SemanticKernelWrapper>();
        services.AddSingleton(kernel);

        var serviceProvider = services.BuildServiceProvider();

        //you do not need to index a document each time you start the software
        await ManageIndexingOfDocuments(kernelMemory);

        var vectorDb = serviceProvider.GetRequiredService<IMemoryDb>();
        var storage = serviceProvider.GetRequiredService<SimpleFileStorageConfig>();

        var textGenerator = serviceProvider.GetRequiredService<ITextGenerator>();
        var searchClientConfig = serviceProvider.GetService<SearchClientConfig>();
        var promptProvider = serviceProvider.GetService<IPromptProvider>();

        var questionPipelineFactory = serviceProvider.GetRequiredService<IUserQuestionPipelineFactory>();
        var questionPipeline = questionPipelineFactory.Create();

        // now ask a question to the user continuously until the user ask an empty question
        string? question;
        UserQuestion userQuestion = null;
        var contextAccessor = serviceProvider.GetRequiredService<IContextProvider>();
        do
        {
            bool shouldDumpRewrittenQuery = false;
            question = AnsiConsole.Ask<string>("Ask a question to the kernel memory:");
            if (!string.IsNullOrWhiteSpace(question))
            {
                var keywordValidation = AnsiConsole.Ask<string>("Specify string to search in text for validation, empty to avoid validation:", "");
                if (userQuestion == null)
                {
                    var options = new UserQueryOptions("default");
                    userQuestion = new UserQuestion(options, question);
                    userQuestion.Context = "Computer security and IT Security";
                }
                else
                {
                    userQuestion.ContinueConversation(question);
                    shouldDumpRewrittenQuery = true;
                }
                var questionEnumerator = questionPipeline.ExecuteQueryAsync(userQuestion);

                Console.WriteLine("\nAnswerStream:\n");
                int segments = 0;
                await foreach (var step in questionEnumerator)
                {
                    if (shouldDumpRewrittenQuery)
                    {
                        Console.WriteLine("Rewritten query: " + userQuestion.Question);
                        shouldDumpRewrittenQuery = false;
                    }
                    if (step.Type == UserQuestionProgressType.AnswerPart)
                    {
                        Console.Write(step.Text);
                        segments++;
                    }
                }

                //ok we really have streaming result?^
                if (segments == 0)
                {
                    //ok we have no streaming, so we need to get the whole answer.
                    Console.Write(userQuestion.Answer);
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
                else if (userQuestion.Citations?.Count > 0)
                {
                    Console.WriteLine("Citations:");
                    foreach (var citation in userQuestion.Citations)
                    {
                        Console.WriteLine("Document: {0}", citation.DocumentId);
                    }
                }

                //ask if we want details
                var details = AnsiConsole.Confirm("Do you want to see the details of the question? (y/n)", false);
                if (details)
                {
                    if (userQuestion.CallContext != null)
                    {
                        Console.WriteLine("Number of LLM calls: {0}", userQuestion.CallContext.CallLogs.Count);
                        foreach (var call in userQuestion.CallContext.CallLogs)
                        {
                            Console.WriteLine("\nCall Name: {0}", call.CallName);
                            Console.WriteLine("Prompt: {0}\n\n", call.InputPrompt);
                            Console.WriteLine("Output: {0}", call.Output);
                            if (call.TokenCount != null)
                            {
                                Console.WriteLine(
                                    "\n******Token Count: Input: {0} Output: {1} CachedRead: {2} CachedWrite {3}*****",
                                    call.TokenCount.InputTokens,
                                    call.TokenCount.OutputTokens,
                                    call.TokenCount.CachedTokenRead,
                                    call.TokenCount.CachedTokenWrite);
                            }

                            if (call.Warnings.Count > 0)
                            {
                                Console.WriteLine("Warnings:");
                                foreach (var warning in call.Warnings)
                                {
                                    Console.WriteLine(warning);
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No details available");
                    }
                }
            }
        } while (!string.IsNullOrWhiteSpace(question));
    }

    private static async Task ManageIndexingOfDocuments(MemoryServerless kernelMemory)
    {
        var indexDocument = AnsiConsole.Confirm("Do you want to index documents? (y/n)", false);
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
        bool useElasticSearch,
        string ragToolToUse,
        bool useHandlebarQueryRewriter,
        bool useHyde)
    {
        // we need a series of services to use Kernel Memory, the first one is
        // an embedding service that will be used to create dense vector for
        // pieces of test. We can use standard ADA embedding service
        var embeddingConfig = new AzureOpenAIConfig
        {
            APIKey = Dotenv.Get("OPENAI_API_KEY") ?? throw new ConfigurationException("OPENAI_API_KEY missing from .env file"),
            Deployment = "text-embedding-ada-002",
            Endpoint = Dotenv.Get("AZURE_ENDPOINT") ?? throw new ConfigurationException("AZURE_ENDPOINT missing from .env file"),
            APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey
        };

        // Now kenel memory needs the LLM data to be able to pass question
        // and retreived segments to the model. We can Use GPT35
        var chatConfig = new AzureOpenAIConfig
        {
            APIKey = Dotenv.Get("OPENAI_API_KEY") ?? throw new ConfigurationException("OPENAI_API_KEY missing from .env file"),
            Deployment = Dotenv.Get("KERNEL_MEMORY_DEPLOYMENT_NAME") ?? throw new ConfigurationException("KERNEL_MEMORY_DEPLOYMENT_NAME missing from .env file"),
            Endpoint = Dotenv.Get("AZURE_ENDPOINT") ?? throw new ConfigurationException("AZURE_ENDPOINT missing from .env file"),
            APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
            Auth = AzureOpenAIConfig.AuthTypes.APIKey,
            MaxTokenTotal = 4096,
        };

        //I need to override the tokenizer because kernel memory uses older version of Microsoft.Ml.Tokenizer
        var textGenerationTokenizer = new MicrosoftMlTiktokenTokenizer("gpt-4o");
        var embeddingTokenizer = new MicrosoftMlTiktokenTokenizer("text-embedding-ada-002");
        var kernelMemoryBuilder = new KernelMemoryBuilder(services)
            .WithAzureOpenAITextGeneration(chatConfig, textGenerationTokenizer)
            .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig, embeddingTokenizer);

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

        services.AddSingleton<HandlebarSemanticKernelQueryRewriter>();
        services.AddSingleton<SemanticKernelQueryRewriter>();
        services.AddSingleton<StandardVectorSearchQueryHandler>();
        services.AddSingleton<HyDeQueryHandler>();
        var hydeConfig = new HiDeQueryHandlerConfiguration()
        {
            Prompt = "Given a question, generate a paragraph of text that answers the question"
        };
        services.AddSingleton(hydeConfig);
        services.AddSingleton<KeywordSearchQueryHandler>();

        var rewriterOptions = new SemanticKernelQueryRewriterOptions();
        rewriterOptions.Temperature = 0.0f;
        services.AddSingleton(rewriterOptions);

        //Register query executors
        services.AddSingleton<CohereCommandRQueryExecutor>();
        services.AddSingleton<StandardRagQueryExecutor>();

        //register openai RAG component
        var openaiRagQueryExecutorConfiguration = new OpenAIRagQueryExecutorConfiguration()
        {
            MaxTokens = 8000,
            Temperature = 0.0,
            ModelId = "gpt4o",
            ModelName = "gpt-4o" //important it will determine the tokenizer
        };
        services.AddSingleton(openaiRagQueryExecutorConfiguration);
        services.AddSingleton<OpenaiRagQueryExecutor>();

        //now create the pipeline
        services.AddKernelMemoryUserQuestionPipeline(config =>
        {
            config.AddHandler<StandardVectorSearchQueryHandler>();
            if (useElasticSearch)
            {
                //I can use keyword search
                config.AddHandler<KeywordSearchQueryHandler>();
            }

            if (useHyde)
            {
                config.AddHandler<HyDeQueryHandler>();
            }

            if (ragToolToUse == "Cohere CommandR+")
            {
                config.AddHandler<CohereCommandRQueryExecutor>();
            }
            else if (ragToolToUse == "OpenAI Tool")
            {
                config.AddHandler<OpenaiRagQueryExecutor>();
            }
            else
            {
                config.AddHandler<StandardRagQueryExecutor>();
            }

            config.SetReRanker<CohereReRanker>();

            if (useHandlebarQueryRewriter)
            {
                config.SetQueryRewriter<HandlebarSemanticKernelQueryRewriter>();
            }
            else
            {
                config.SetQueryRewriter<SemanticKernelQueryRewriter>();
            }
        });
        return kernelMemoryBuilder;
    }

    /// <summary>
    /// Create Kernel Memory builder because it is used to inteact with the LLM to perform
    /// conversation.
    /// </summary>
    /// <returns></returns>
    private static IKernelBuilder CreateBasicKernelBuilder()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(l => l
            .SetMinimumLevel(LogLevel.Trace)
            //.AddConsole()
            .AddDebug()
            .AddProvider(_loggingProvider)
        );

        kernelBuilder.Services.ConfigureHttpClientDefaults(c => c
            .AddLogger(s => _loggingProvider.CreateHttpRequestBodyLogger(s.GetRequiredService<ILogger<DumpLoggingProvider>>())));

        kernelBuilder.Services.AddAzureOpenAIChatCompletion(
            "GPT35_2",
            Dotenv.Get("OPENAI_API_BASE") ?? throw new ConfigurationException("OPENAI_API_BASE missing from .env file"),
            Dotenv.Get("OPENAI_API_KEY") ?? throw new ConfigurationException("OPENAI_API_KEY missing from .env file"),
            serviceId: "gpt35",
            modelId: "gpt35");

        kernelBuilder.Services.AddAzureOpenAIChatCompletion(
            "GPT4o", //"GPT35_2",//"GPT42",
            Dotenv.Get("OPENAI_API_BASE") ?? throw new ConfigurationException("OPENAI_API_BASE missing from .env file"),
            Dotenv.Get("OPENAI_API_KEY") ?? throw new ConfigurationException("OPENAI_API_KEY missing from .env file"),
            serviceId: "default",
            modelId: "gpt4o");

        return kernelBuilder;
    }
}
