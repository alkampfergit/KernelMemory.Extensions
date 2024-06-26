﻿using KernelMemory.ElasticSearch.Anthropic;
using KernelMemory.Extensions.ConsoleTest.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

namespace SemanticMemory.Samples
{
    internal class AnthropicSample : ISample
    {
        public async Task RunSample(string bookPdf)
        {
            var services = new ServiceCollection();
            services.AddHttpClient();
            var builder = CreateAnthropicKernelMemoryBuilder(services, useMongoDbAtlas: false);

            var kernelMemory = builder.Build<MemoryServerless>();

            var serviceProvider = services.BuildServiceProvider();

            var docId = Path.GetFileName(bookPdf);

            //you do not need to index a document each time you start the software
            Console.WriteLine("Do you want to index a document? (y/n)");
            var answer = Console.ReadLine();
            if (answer == "y")
            {
                await IndexDocument(kernelMemory, bookPdf, docId);
            }

            var vectorDb = serviceProvider.GetRequiredService<IMemoryDb>();
#pragma warning disable KMEXP03 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var storage = serviceProvider.GetRequiredService<SimpleFileStorage>();
#pragma warning restore KMEXP03 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            //you can also retrieve the document from the storage
            var document = await storage.ReadFileAsync("default", docId, Path.GetFileName(bookPdf));

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

        private static IKernelMemoryBuilder CreateAnthropicKernelMemoryBuilder(
            ServiceCollection services,
            bool useMongoDbAtlas = false)
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
            var chatConfig = new AnthropicTextGenerationConfiguration()
            {
                ApiKey = Dotenv.Get("ANTHROPIC_API_KEY"),
                MaxTokenTotal = 4096
            };

            var kernelMemoryBuilder = new KernelMemoryBuilder(services)
                .WithAnthropicTextGeneration(chatConfig)
                .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig);

            if (useMongoDbAtlas)
            {
                var mongoConnection = Dotenv.Get("MONGO_CONNECTION");
                //var builder = new MongoUrlBuilder(mongoConnection);
                //var client = new MongoClient(builder.ToMongoUrl());
                //var db = client.GetDatabase("kernelMemory");
                //IContentStorage mongoStorage = new MongoDbStorage(db);
                //IMemoryDb mongoVectorMemory = new MongoDbVectorMemory(db);

                //var config = new MongoDbKernelMemoryConfiguration()
                //    .WithConnection(mongoConnection)
                //    .WithDatabaseName("TestKernelMemory")
                //    .WithSingleCollectionForVectorSearch(true);

                //kernelMemoryBuilder
                //     .WithAtlasMemoryDb(config);
            }
            else
            {
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
            }

            // kernelMemoryBuilder.Services.ConfigureHttpClientDefaults(c => c
            //     .AddLogger(s => _loggingProvider.CreateHttpRequestBodyLogger(s.GetRequiredService<ILogger<DumpLoggingProvider>>())));

            services.AddSingleton<IKernelMemoryBuilder>(kernelMemoryBuilder);
            return kernelMemoryBuilder;
        }
    }
}
