using KernelMemory.Extensions.Cohere;
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