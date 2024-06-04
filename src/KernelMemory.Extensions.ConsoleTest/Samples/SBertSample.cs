using KernelMemory.Extensions.ConsoleTest.Helper;
using KernelMemory.Extensions.LocalLlm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using SemanticMemory.Helper;

namespace SemanticMemory.Samples;

internal class SBertSample : ISample
{
    public async Task RunSample(string bookPdf)
    {
        //This sample recreates completely the connection
        var services = new ServiceCollection();

        // ask use if they want to use a local llm
        Console.WriteLine("Do you want to use a local model?");
        var key = Console.ReadKey();
        var useLocal = key.KeyChar == 'y';

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

        services.AddHttpClient();

        //if we want to use a local llm we simply need to use openai configuration but then redirect to http://localhost:1234/v1/chat/completions

        var serviceLocator = services.BuildServiceProvider();
        var httpClientFactory = serviceLocator.GetRequiredService<IHttpClientFactory>();
        var kernelMemoryBuilder = new KernelMemoryBuilder(services);

        if (useLocal)
        {
            kernelMemoryBuilder.WithCustomTextGenerator(
                new LmStudioTextGeneration(
                    httpClientFactory,
                    new Uri("http://localhost:1234")));
        }
        else
        {
            kernelMemoryBuilder.WithAzureOpenAITextGeneration(chatConfig);
        }

        var embeddingConfig = new ExternalEmbeddingGeneratorConfig()
        {
            ModelName = "msmarco-distilbert-base-v4"
        };

        kernelMemoryBuilder
            .WithCustomEmbeddingGenerator(
                new ExternalEmbeddingGenerator(
                    httpClientFactory,
                    embeddingConfig))
            .WithSimpleFileStorage(new SimpleFileStorageConfig()
            {
                Directory = "c:\\temp\\kmsbert\\storage",
                StorageType = FileSystemTypes.Disk
            })
           .WithSimpleVectorDb(new SimpleVectorDbConfig()
           {
               Directory = "c:\\temp\\kmsbert\\vectorstorage",
               StorageType = FileSystemTypes.Disk
           })
            .WithCustomTextPartitioningOptions(new TextPartitioningOptions
            {
                // Max 150 tokens per sentence
                MaxTokensPerLine = 150,
                // When sentences are merged into paragraphs (aka partitions), stop at 384 tokens
                MaxTokensPerParagraph = 384,
                // Each paragraph contains the last 30 tokens from the previous one
                OverlappingTokens = 30,
            }
            );

        var kernelMemory = kernelMemoryBuilder.Build<MemoryServerless>();

        var doc = bookPdf;

        var docId = Path.GetFileName(doc);
        await IndexDocument(kernelMemory, doc, docId);

        string question;
        do
        {
            Console.WriteLine("Ask a question to the kernel memory. write q or quit to exit:");
            question = Console.ReadLine();
            if (question == "q" || question == "quit")
            {
                break;
            }
            if (!string.IsNullOrWhiteSpace(question))
            {
                var response = await kernelMemory.AskAsync(question);
                Console.WriteLine(response.Result);
            }
        } while (true);
    }

    private static async Task IndexDocument(MemoryServerless kernelMemory, string doc, string docId)
    {
        var importDocumentTask = kernelMemory.ImportDocumentAsync(doc, docId);

        while (!importDocumentTask.IsCompleted)
        {
            var docStatus = await kernelMemory.GetDocumentStatusAsync(docId);
            if (docStatus != null)
            {
                Console.WriteLine("Completed Steps:" + string.Join(",", docStatus.CompletedSteps));
            }

            await Task.Delay(1000);
        }
    }
}
