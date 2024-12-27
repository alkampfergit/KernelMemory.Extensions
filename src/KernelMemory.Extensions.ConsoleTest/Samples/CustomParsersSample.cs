using KernelMemory.Extensions.ConsoleTest.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.KernelMemory.Pipeline;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using HandlebarsDotNet.Extensions;

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

        CustomSamplePartitioningHandler customMarkdownPartition = new("markdownpartition", orchestrator);
        await orchestrator.AddHandlerAsync(customMarkdownPartition);

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
            //.Then("partition")
            .Then("markdownpartition")
            .Then("gen_embeddings")
            .Then("save_records");

        contextProvider.AddLLamaCloudParserOptions(fileName, "This is a manual for Dreame vacuum cleaner, I need you to extract a series of sections that can be useful for an helpdesk to answer user questions. You will create sections where each sections contains a question and an answer taken from the text. Each question will be separated with ---");

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

public sealed class CustomSamplePartitioningHandler : IPipelineStepHandler
{
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly ILogger<TextPartitioningHandler> _log;

    /// <inheritdoc />
    public string StepName { get; }

    /// <summary>
    /// Handler responsible for partitioning text in small chunks.
    /// Note: stepName and other params are injected with DI.
    /// </summary>
    /// <param name="stepName">Pipeline step for which the handler will be invoked</param>
    /// <param name="orchestrator">Current orchestrator used by the pipeline, giving access to content and other helps.</param>
    /// <param name="options">The customize text partitioning option</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public CustomSamplePartitioningHandler(
        string stepName,
        IPipelineOrchestrator orchestrator,
        ILoggerFactory? loggerFactory = null)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;

        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TextPartitioningHandler>();
        this._log.LogInformation("Handler '{0}' ready", stepName);
    }

    /// <inheritdoc />
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        this._log.LogDebug("Markdown question Partitioning text, pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

        if (pipeline.Files.Count == 0)
        {
            this._log.LogWarning("Pipeline '{0}/{1}': there are no files to process, moving to next pipeline step.", pipeline.Index, pipeline.DocumentId);
            return (ReturnType.Success, pipeline);
        }

        var context = pipeline.GetContext();

        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
            Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles = [];

            foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
            {
                var file = generatedFile.Value;
                if (file.AlreadyProcessedBy(this))
                {
                    this._log.LogTrace("File {0} already processed by this handler", file.Name);
                    continue;
                }

                // Partition only the original text
                if (file.ArtifactType != DataPipeline.ArtifactTypes.ExtractedText)
                {
                    this._log.LogTrace("Skipping file {0} (not original text)", file.Name);
                    continue;
                }

                // Use a different partitioning strategy depending on the file type
                BinaryData partitionContent = await this._orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false);
                string partitionsMimeType = MimeTypes.MarkDown;

                // Skip empty partitions. Also: partitionContent.ToString() throws an exception if there are no bytes.
                if (partitionContent.IsEmpty) { continue; }
                int partition = 1;
                switch (file.MimeType)
                {
                    case MimeTypes.MarkDown:
                        {
                            this._log.LogDebug("Partitioning MarkDown file {0}", file.Name);
                            string content = partitionContent.ToString();
                            partitionsMimeType = MimeTypes.MarkDown;

                            var sb = new StringBuilder(1024);
                            using (var reader = new StringReader(content))
                            {
                                string? line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                    {
                                        continue;
                                    }

                                    if (line.StartsWith("---"))
                                    {
                                        partition = await AddSegment(pipeline, uploadedFile, newFiles, partitionsMimeType, partition, sb, cancellationToken).ConfigureAwait(false);
                                        sb.Clear();
                                        continue;
                                    }

                                    sb.AppendLine(line);
                                }
                            }

                            // Write remaining content if any
                            if (sb.Length > 0)
                            {
                                await AddSegment(pipeline, uploadedFile, newFiles, partitionsMimeType, partition, sb, cancellationToken).ConfigureAwait(false);
                            }

                            break;
                        }

                    default:
                        this._log.LogWarning("File {0} cannot be partitioned, type '{1}' not supported", file.Name, file.MimeType);
                        // Don't partition other files
                        continue;
                }
            }

            // Add new files to pipeline status
            foreach (var file in newFiles)
            {
                uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
            }
        }

        return (ReturnType.Success, pipeline);
    }

    private async Task<int> AddSegment(DataPipeline pipeline, DataPipeline.FileDetails uploadedFile, Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles, string partitionsMimeType, int partition, StringBuilder sb, CancellationToken cancellationToken)
    {
        if (sb.Length == 0)
        {
            //do not increment partition, an empty segment is not a segment.
            return partition;
        }

        var text = sb.ToString().Trim('\n', '\r', ' ');

        //is empty after trimming?
        if (string.IsNullOrWhiteSpace(text))
        {
            //do not increment partition, an empty segment is not a segment.
            return partition;
        }

        var destFile = uploadedFile.GetPartitionFileName(partition);
        var textData = new BinaryData(sb.ToString());
        await this._orchestrator.WriteFileAsync(pipeline, destFile, textData, cancellationToken).ConfigureAwait(false);

        var destFileDetails = new DataPipeline.GeneratedFileDetails
        {
            Id = Guid.NewGuid().ToString("N"),
            ParentId = uploadedFile.Id,
            Name = destFile,
            Size = sb.Length,
            MimeType = partitionsMimeType,
            ArtifactType = DataPipeline.ArtifactTypes.TextPartition,
            PartitionNumber = partition,
            SectionNumber = 1,
            Tags = pipeline.Tags,
            ContentSHA256 = textData.CalculateSHA256(),
        };
        newFiles.Add(destFile, destFileDetails);
        destFileDetails.MarkProcessedBy(this);
        partition++;
        return partition;
    }
}

internal static class BinaryDataExtensions
{
    public static string CalculateSHA256(this BinaryData binaryData)
    {
        byte[] byteArray = SHA256.HashData(binaryData.ToMemory().Span);
        return Convert.ToHexString(byteArray).ToLowerInvariant();
    }
}
