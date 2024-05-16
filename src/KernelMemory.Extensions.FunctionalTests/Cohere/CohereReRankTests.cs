using CommandDotNet.Execution;
using KernelMemory.Extensions.Cohere;
using KernelMemory.Extensions.FunctionalTests.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.MemoryStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.FunctionalTests.Cohere;

public class CohereReRankTests
{
    private IHttpClientFactory _ihttpClientFactory;

    public CohereReRankTests()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var serviceProvider = services.BuildServiceProvider();
        _ihttpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public async Task Basic_cohere_reranking()
    {
        var cohereConfig = new CohereConfiguration
        {
            //ApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY"),
            ApiKey = "4F9DspZG4KbIGTL5z4kb4Sid480iCgjxjObdPorh",
        };
        var cohereClient = new RawCohereClient(cohereConfig, _ihttpClientFactory);
        var ReRankResult = await cohereClient.ReRankAsync(new CohereReRankRequest("What is the capital of the United States?",
            ["Carson City is the capital city of the American state of Nevada.",
                  "The Commonwealth of the Northern Mariana Islands is a group of islands in the Pacific Ocean. Its capital is Saipan.",
                  "Washington, D.C. (also known as simply Washington or D.C., and officially as the District of Columbia) is the capital of the United States. It is a federal district.",
                  "Capital punishment (the death penalty) has existed in the United States since beforethe United States was a country. As of 2017, capital punishment is legal in 30 of the 50 states."]));

        Assert.NotNull(ReRankResult);
        Assert.Equal([2, 3, 0, 1], ReRankResult.Results.Select(r => r.Index));
    }

    [Fact]
    public async Task Basic_cohere_Rag_streaming()
    {
        var cohereConfig = new CohereConfiguration
        {
            //ApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY"),
            ApiKey = "4F9DspZG4KbIGTL5z4kb4Sid480iCgjxjObdPorh",
        };
        var cohereClient = new RawCohereClient(cohereConfig, _ihttpClientFactory);

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
        var cohereConfig = new CohereConfiguration
        {
            //ApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY"),
            ApiKey = "4F9DspZG4KbIGTL5z4kb4Sid480iCgjxjObdPorh",
        };
        var cohereClient = new RawCohereClient(cohereConfig, _ihttpClientFactory);

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
}
