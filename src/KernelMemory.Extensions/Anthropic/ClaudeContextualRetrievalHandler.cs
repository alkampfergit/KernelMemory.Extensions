using KernelMemory.ElasticSearch.Anthropic;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.ConsoleTest.Helper;

public class ClaudeContextualRetrievalHandler : IPipelineStepHandler
{
    private string _name;
    private IPipelineOrchestrator _orchestrator;
    private readonly RawAnthropicClient _rawAnthropicHttpClient;
    private readonly ILogger<ClaudeContextualRetrievalHandler> _log;

    public ClaudeContextualRetrievalHandler(
        string name,
        IPipelineOrchestrator orchestrator,
        RawAnthropicClient rawAnthropicHttpClient,
        ILogger<ClaudeContextualRetrievalHandler>? log = null)
    {
        _name = name;
        _orchestrator = orchestrator;
        _rawAnthropicHttpClient = rawAnthropicHttpClient;
        _log = log ?? DefaultLogger<ClaudeContextualRetrievalHandler>.Instance; ;
    }

    public string StepName => _name;

    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken)
    {
        _log.LogDebug("Partitioning text, pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

        if (pipeline.Files.Count == 0)
        {
            _log.LogWarning("Pipeline '{0}/{1}': there are no files to process, moving to next pipeline step.", pipeline.Index, pipeline.DocumentId);
            return (ReturnType.Success, pipeline);
        }

        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
            Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles = new();
            // we need to find the original extracted text
            var textFile = uploadedFile.GeneratedFiles.FirstOrDefault(f => f.Value.ArtifactType == DataPipeline.ArtifactTypes.ExtractedText);

            if (textFile.Value != null)
            {
                var contentFile = await _orchestrator.ReadFileAsync(pipeline, textFile.Value.Name, cancellationToken).ConfigureAwait(false);
                var text = contentFile.ToString();
                foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
                {
                    var file = generatedFile.Value;
                    if (file.AlreadyProcessedBy(this))
                    {
                        _log.LogTrace("File {0} already processed by this handler", file.Name);
                        continue;
                    }

                    if (file.ArtifactType != DataPipeline.ArtifactTypes.TextPartition)
                    {
                        _log.LogTrace("Skipping file {0} (not text partition)", file.Name);
                        continue;
                    }

                    var content = await _orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false);
                    if (content.ToArray().Length == 0) { continue; }

                    var segmentContent = content.ToString();
                    var contextGenerated = await EnrichTextAsync(text, segmentContent, cancellationToken);
                    var enrichedText = contextGenerated + "\n" + segmentContent;
                    await _orchestrator.WriteTextFileAsync(pipeline, file.Name, enrichedText, cancellationToken).ConfigureAwait(false);

                    file.MarkProcessedBy(this);
                }

                // Add new files to pipeline status
                foreach (var file in newFiles)
                {
                    uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
                }
            }
        }
        return (ReturnType.Success, pipeline);
    }

    private async Task<string> EnrichTextAsync(
        string text,
        string segment,
         CancellationToken cancellationToken)
    {
        var parameters = new CallClaudeStreamingParams()
        {
            ModelName = AnthropicTextGenerationConfiguration.HaikuModelName,
            System = [
                SystemMessage.Create(@$"<document> 
{text}
</document>", CacheControl.Ephemeral)],
            Messages = [Message.Create("user", @$"Here is the chunk we want to situate within the whole document 
<chunk> 
{segment} 
</chunk> 
Please give a short succinct context to situate this chunk within the overall document for the purposes of improving search retrieval of the chunk. Answer only with the succinct context and nothing else. ")],
            MaxTokens = 5,
        };

        var result = await _rawAnthropicHttpClient.CallClaudeAsync(parameters, cancellationToken);
        return result.Content[0].Text;
    }
}