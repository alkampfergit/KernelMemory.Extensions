using Fasterflect;
using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.FunctionalTests.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.MemoryStorage;

namespace KernelMemory.Extensions.FunctionalTests.Cohere;

public class CohereTests
{
    private ServiceProvider _serviceProvider;

    private IHttpClientFactory _httpClientFactory;

    public CohereTests()
    {
        var services = new ServiceCollection();
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

        var cohereApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY");

        if (string.IsNullOrEmpty(cohereApiKey))
        {
            throw new Exception("COHERE_API_KEY is not set");
        }

        services.ConfigureCohere(cohereApiKey);

        _serviceProvider = services.BuildServiceProvider();
        _httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public async Task Basic_cohere_reranking()
    {
        var cohereClient = new RawCohereClient(_serviceProvider);
        var ReRankResult = await cohereClient.ReRankAsync(new CohereReRankRequest("What is the capital of the United States?",
            ["Carson City is the capital city of the American state of Nevada.",
                  "The Commonwealth of the Northern Mariana Islands is a group of islands in the Pacific Ocean. Its capital is Saipan.",
                  "Washington, D.C. (also known as simply Washington or D.C., and officially as the District of Columbia) is the capital of the United States. It is a federal district.",
                  "Capital punishment {}{} (the death penalty) has existed in the United States since beforethe United States was a country. As of 2017, capital punishment is legal in 30 of the 50 states."]));

        Assert.NotNull(ReRankResult);
        Assert.Equal([2, 3, 0, 1], ReRankResult.Results.Select(r => r.Index));
    }

    [Fact]
    public async Task Can_rerank_empty_document_list()
    {
        var cohereClient = new RawCohereClient(_serviceProvider);
        var ReRankResult = await cohereClient.ReRankAsync(new CohereReRankRequest("What is the capital of the United States?", []));

        Assert.NotNull(ReRankResult);
        Assert.True(ReRankResult.Results.Count == 0);
    }

    [Fact]
    public async Task Basic_cohere_Rag_streaming()
    {
        var cohereClient = new RawCohereClient(_serviceProvider);

        var records = new List<MemoryRecord>();
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 1, "Carson City is the capital city of the American state of Nevada."));
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc2", "file1", 2, "The Commonwealth of the Northern Mariana Islands is a group of islands in the Pacific Ocean. Its capital is Saipan."));
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc3", "file1", 3, "Washington, D.C. (also known as simply Washington or D.C., and officially as the District of Columbia) is the capital of the United States. It is a federal district."));
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc4", "file1", 4, "Capital punishment (the death penalty) has existed in the United States since before the United States was a country. As of 2017, capital punishment is legal in 30 of the 50 states."));

        var cohereRagRequest = CohereRagRequest.CreateFromMemoryRecord("What is the capital of the United States?", records);

        var asiterator = cohereClient.RagQueryStreamingAsync(cohereRagRequest);
        var list = await asiterator.ToListAsync();
    }

    [Fact]
    public async Task Basic_cohere_Rag()
    {
        var cohereClient = new RawCohereClient(_serviceProvider);

        var records = new List<MemoryRecord>();
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc1", "file1", 1, "Carson City is the capital city of the American state of Nevada."));
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc2", "file1", 2, "The Commonwealth of the Northern Mariana Islands is a group of islands in the Pacific Ocean. Its capital is Saipan."));
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc3", "file1", 3, "Washington, D.C. (also known as simply Washington or D.C., and officially as the District of Columbia) is the capital of the United States. It is a federal district."));
        records.Add(MemoryRecordTestUtilities.CreateMemoryRecord("doc4", "file1", 4, "Capital punishment (the death penalty) has existed in the United States since before the United States was a country. As of 2017, capital punishment is legal in 30 of the 50 states."));

        var cohereRagRequest = CohereRagRequest.CreateFromMemoryRecord("What is the capital of the United States?", records);
        cohereRagRequest.Temperature = 0;
        cohereRagRequest.MaxInputTokens = 15000;

        var ragResponse = await cohereClient.RagQueryAsync(cohereRagRequest);
        Assert.NotNull(ragResponse.Text);
        Assert.True(ragResponse.Citations.Count > 1);
    }

    [Fact]
    public async Task Basic_cohere_embed_test()
    {
        var cohereClient = new RawCohereClient(_serviceProvider);

        var embedRequest = new CohereEmbedRequest
        {
            Texts = new List<string> { "example text 1", "example text 2" },
            Model = CohereModels.EmbedEnglishV3,
            InputType = CohereInputTypes.Classification,
            EmbeddingTypes = new List<string> { "float" },
            Truncate = "END"
        };

        var embedResult = await cohereClient.EmbedAsync(embedRequest);
        Assert.Equal(2, embedResult.Embeddings.Values.Count);
        Assert.Equal(1024, embedResult.Embeddings.Values[0].Length);
    }

    [Fact]
    public void Tokenizer_raw_test()
    {
        CohereTokenizer tokenizer = new(_httpClientFactory);
        var count = tokenizer.CountToken("command-r-plus", "Now I'm using CommandR+ tokenizer");
        Assert.Equal(8, count);
    }

    // /// <summary>
    // /// In azure ai studio we do not still have re-ranking, so we need to use re-ranker with a configuration
    // /// and the executor with another configuration
    // /// </summary>
    // [Fact]
    // public void Ability_to_use_azure()
    // {
    //     var configReRank = new CohereConfiguration()
    //     {
    //         ApiKey = "Base Api Key",
    //     };
    //     var configRagExecutor = new CohereConfiguration()
    //     {
    //         ApiKey = "Azure configuration",
    //         BaseUrl = "https://api.azure.cohere.ai/",
    //     };

    //     var services = new ServiceCollection();
    //     services.AddHttpClient();
    //     services.AddKeyedSingleton<CohereConfiguration>("rerank", configReRank);
    //     services.AddKeyedSingleton<CohereConfiguration>("executor", configRagExecutor);

    //     services.AddKeyedSingleton<RawCohereClient>("rerank", (sp, key) =>
    //     {
    //         var options = sp.GetKeyedService<CohereConfiguration>(key);
    //         var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    //         return new RawCohereClient(options, _serviceProvider);
    //     });
    //     services.AddKeyedSingleton<RawCohereClient>("executor", (sp, key) =>
    //     {
    //         var options = sp.GetKeyedService<CohereConfiguration>(key);
    //         var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    //         return new RawCohereClient(options, _serviceProvider);
    //     });

    //     var serviceProvider = services.BuildServiceProvider();
    //     var cohereConfigurationReRank = serviceProvider.GetKeyedService<CohereConfiguration>("rerank");
    //     var cohereConfigurationExecutor = serviceProvider.GetKeyedService<CohereConfiguration>("executor");

    //     var cohereClientReRank = serviceProvider.GetKeyedService<RawCohereClient>("rerank");
    //     var cohereClientExecutor = serviceProvider.GetKeyedService<RawCohereClient>("executor");

    //     //Base assertion, you can simply get by key
    //     Assert.Equal("Azure configuration", cohereConfigurationExecutor.ApiKey);
    //     Assert.Equal("https://api.azure.cohere.ai/", cohereConfigurationExecutor.BaseUrl);

    //     Assert.Equal("https://api.cohere.ai/", cohereConfigurationReRank.BaseUrl);
    //     Assert.Equal("Base Api Key", cohereConfigurationReRank.ApiKey);

    //     //now verify the two clients
    //     Assert.Equal(cohereClientReRank.GetFieldValue("_apiKey"), cohereConfigurationReRank.ApiKey);

    // }
}
