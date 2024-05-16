using KernelMemory.Extensions.ConsoleTest.Helper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using SemanticMemory.Samples;
using Spectre.Console;
using System.Text.Json;

namespace KernelMemory.Extensions.ConsoleTest;

public static class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<SimpleBookIndexingWithTextCleaning>();
        services.AddSingleton<SBertSample>();
        services.AddSingleton<BasicSample>();
        services.AddSingleton<TextCleanerHandler>();
        services.AddSingleton<CustomSearchPipelineBase>();
        services.AddSingleton<AnthropicSample>();
        services.AddHttpClient();

        var sp = services.BuildServiceProvider();
        var httpFactory = sp.GetService<IHttpClientFactory>();
        await TestCohere(httpFactory);

        var serviceProvider = services.BuildServiceProvider();

        // Ask for the user's favorite fruits
        var choices = new Dictionary<string, Type?>
        {
            ["Basic Sample"] = typeof(SimpleBookIndexingWithTextCleaning),
            ["Custom Pipeline (text cleaner)"] = typeof(SimpleBookIndexingWithTextCleaning),
            ["SBert in action"] = typeof(SBertSample),
            ["Custom Search pipeline (Basic)"] = typeof(CustomSearchPipelineBase),
            ["Anthropic"] = typeof(AnthropicSample),
            ["Exit"] = null
        };

        Type? sampleType;
        do
        {
            var sample = AnsiConsole.Prompt(
              new SelectionPrompt<string>()
                  .Title("Choose the option to run?")
                  .PageSize(10)
                  .MoreChoicesText("[grey](Move up and down to select the example)[/]")
                  .AddChoices(choices.Keys.ToArray()));

            sampleType = choices[sample];
            if (sampleType != null)
            {
                var sampleInstance = serviceProvider.GetRequiredService(sampleType);
                if (sampleInstance is ISample2 sampleInstance2)
                {
                    await sampleInstance2.RunSample2();
                }
                else if (sampleInstance is ISample sampleInstance1)
                {
                    var book = AnsiConsole.Prompt(new SelectionPrompt<string>()
                        .Title("Select the [green]book[/] to index")
                        .AddChoices([@"c:\temp\advancedapisecurity.pdf", @"S:\OneDrive\B19553_11.pdf"]));
                    await sampleInstance1.RunSample(book);
                }
            }
        } while (sampleType != null);
    }

    private static async Task TestCohere(IHttpClientFactory? httpFactory)
    {
        var cohereConfig = new Cohere.CohereConfiguration
        {
            ApiKey = Dotenv.Get("COHERE_API_KEY"),
        };
        var rr = new Cohere.RawCohereClient(cohereConfig, httpFactory);

        var records = new List<MemoryRecord>();
        records.Add(CreateMemoryRecord("doc1", "file1", 1, "Carson City is the capital city of the American state of Nevada."));
        records.Add(CreateMemoryRecord("doc2", "file1", 2, "The Commonwealth of the Northern Mariana Islands is a group of islands in the Pacific Ocean. Its capital is Saipan."));
        records.Add(CreateMemoryRecord("doc3", "file1", 3, "Washington, D.C. (also known as simply Washington or D.C., and officially as the District of Columbia) is the capital of the United States. It is a federal district."));
        records.Add(CreateMemoryRecord("doc4", "file1", 4, "Capital punishment (the death penalty) has existed in the United States since before the United States was a country. As of 2017, capital punishment is legal in 30 of the 50 states."));

        var cohereRagRequest = Cohere.CohereRagRequest.CreateFromMemoryRecord("What is the capital of the United States?", records);
        
        var asiterator = rr.RagQueryStreamingAsync(cohereRagRequest);
        var list = await asiterator.ToListAsync();

        //Cohere commandR+ api non streaming
        //var ragQueryResult = await rr.RagQueryAsync(cohereRagRequest);

        ////pretty print the result in console serializing ragQueryResult in json formatted
        //AnsiConsole.Write(JsonSerializer.Serialize(
        //    ragQueryResult,
        //    new JsonSerializerOptions() 
        //    {
        //        WriteIndented = true
        //    })
        // );

        //var ReRankResult = await rr.ReRankAsync(new Cohere.CohereReRankRequest("What is the capital of the United States?",
        //    ["Carson City is the capital city of the American state of Nevada.",
        //          "The Commonwealth of the Northern Mariana Islands is a group of islands in the Pacific Ocean. Its capital is Saipan.",
        //          "Washington, D.C. (also known as simply Washington or D.C., and officially as the District of Columbia) is the capital of the United States. It is a federal district.",
        //          "Capital punishment (the death penalty) has existed in the United States since beforethe United States was a country. As of 2017, capital punishment is legal in 30 of the 50 states."]));


    }

    private static MemoryRecord CreateMemoryRecord(string documentId, string fileId, int partitionNumber, string textPartition)
    {
        var mr = new MemoryRecord();
        mr.Payload = new Dictionary<string, object>();
        mr.Payload["text"] = textPartition;
        mr.Tags = new TagCollection
            {
                { Constants.ReservedDocumentIdTag, documentId },
                { Constants.ReservedFileIdTag, fileId },
                { Constants.ReservedFilePartitionNumberTag, partitionNumber.ToString() }
            };

        return mr;
    }
}