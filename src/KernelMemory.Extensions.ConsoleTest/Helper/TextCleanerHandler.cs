using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using System.Text;

namespace KernelMemory.Extensions.ConsoleTest.Helper;

internal class TextCleanerHandler : IPipelineStepHandler
{
    private string _name;
    private IPipelineOrchestrator _orchestrator;
    private readonly ILogger<TextCleanerHandler> _log;

    public TextCleanerHandler(
        string name,
        IPipelineOrchestrator orchestrator,
        ILogger<TextCleanerHandler> log = null)
    {
        _name = name;
        _orchestrator = orchestrator;
        _log = log ?? DefaultLogger<TextCleanerHandler>.Instance;
    }

    public string StepName => _name;

    public async Task<(bool success, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        _log.LogDebug("Partitioning text, pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

        if (pipeline.Files.Count == 0)
        {
            _log.LogWarning("Pipeline '{0}/{1}': there are no files to process, moving to next pipeline step.", pipeline.Index, pipeline.DocumentId);
            return (true, pipeline);
        }

        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
            Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles = new();

            foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
            {
                var file = generatedFile.Value;
                if (file.AlreadyProcessedBy(this))
                {
                    _log.LogTrace("File {0} already processed by this handler", file.Name);
                    continue;
                }

                // Clean only original text, we need to remove unicode char, weird spaces, etc
                // and we want to work only on the original file, not the already partitioned text.
                if (file.ArtifactType != DataPipeline.ArtifactTypes.ExtractedText)
                {
                    _log.LogTrace("Skipping file {0} (not original text)", file.Name);
                    continue;
                }

                var content = await _orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false);
                if (content.ToArray().Length == 0) { continue; }

                var textContent = content.ToString();

                // we will completely replace the original content
                var newContent = new StringBuilder(textContent.Length);
                foreach (char c in textContent)
                {
                    int b = c;
                    if (b >= 32 && b <= 255 || b == 13 || b == 10 || char.IsPunctuation(c))
                    {
                        newContent.Append(c);
                    }
                }
                await _orchestrator.WriteTextFileAsync(pipeline, file.Name, newContent.ToString(), cancellationToken).ConfigureAwait(false);

                file.MarkProcessedBy(this);
            }

            // Add new files to pipeline status
            foreach (var file in newFiles)
            {
                uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
            }
        }
        return (true, pipeline);
    }
}
