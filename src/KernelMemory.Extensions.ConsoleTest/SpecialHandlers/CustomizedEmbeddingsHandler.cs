using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.ConsoleTest.SpecialHandlers;

/// <summary>
/// This is based on the original embedding handler of kernel memory, it only adds the ability
/// to externalize a transformer that extract from the original text the text that needs to be
/// passed to the embedding generator.
/// </summary>
public sealed class CustomizedEmbeddingsHandler : GenerateEmbeddingsHandlerBase, IPipelineStepHandler
{
    private readonly ILogger<CustomizedEmbeddingsHandler> _log;
    private readonly List<ITextEmbeddingGenerator> _embeddingGenerators;
    private readonly bool _embeddingGenerationEnabled;
    private readonly Func<string, CancellationToken, Task<string>> _extractTextToEmbedAsync;

    /// <inheritdoc />
    public string StepName { get; }

    /// <summary>
    /// Handler responsible for generating embeddings and saving them to document storages (not memory db).
    /// Note: stepName and other params are injected with DI
    /// </summary>
    /// <param name="stepName">Pipeline step for which the handler will be invoked</param>
    /// <param name="orchestrator">Current orchestrator used by the pipeline, giving access to content and other helps.</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public CustomizedEmbeddingsHandler(
        string stepName,
        IPipelineOrchestrator orchestrator,
        Func<string, CancellationToken, Task<string>> extractTextToEmbedAsync,
        ILoggerFactory? loggerFactory = null)
        : base(orchestrator, (loggerFactory ?? DefaultLogger.Factory).CreateLogger<CustomizedEmbeddingsHandler>())
    {
        this.StepName = stepName;
        _extractTextToEmbedAsync = extractTextToEmbedAsync ?? throw new ArgumentNullException(nameof(extractTextToEmbedAsync));

        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<CustomizedEmbeddingsHandler>();
        this._embeddingGenerationEnabled = orchestrator.EmbeddingGenerationEnabled;
        this._embeddingGenerators = orchestrator.GetEmbeddingGenerators();

        if (this._embeddingGenerationEnabled)
        {
            if (this._embeddingGenerators.Count < 1)
            {
                this._log.LogError("Handler '{0}' NOT ready, no embedding generators configured", stepName);
            }

            this._log.LogInformation("Handler '{0}' ready, {1} embedding generators", stepName, this._embeddingGenerators.Count);
        }
        else
        {
            this._log.LogInformation("Handler '{0}' ready, embedding generation DISABLED", stepName);
        }
    }

    /// <inheritdoc />
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        if (!this._embeddingGenerationEnabled)
        {
            this._log.LogTrace("Embedding generation is disabled, skipping - pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);
            return (ReturnType.Success, pipeline);
        }

        foreach (ITextEmbeddingGenerator generator in this._embeddingGenerators)
        {
            var subStepName = GetSubStepName(generator);
            var partitions = await this.GetListOfPartitionsToProcessAsync(pipeline, subStepName, cancellationToken).ConfigureAwait(false);

            int batchSize = pipeline.GetContext().GetCustomEmbeddingGenerationBatchSizeOrDefault((generator as ITextEmbeddingBatchGenerator)?.MaxBatchSize ?? 1);
            if (batchSize > 1 && generator is ITextEmbeddingBatchGenerator batchGenerator)
            {
                await this.GenerateEmbeddingsWithBatchingAsync(pipeline, batchGenerator, batchSize, partitions, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.GenerateEmbeddingsOneAtATimeAsync(pipeline, generator, partitions, cancellationToken).ConfigureAwait(false);
            }
        }

        return (ReturnType.Success, pipeline);
    }

    protected override IPipelineStepHandler ActualInstance => this;

    // Generate and save embeddings, one batch at a time
    private async Task GenerateEmbeddingsWithBatchingAsync(
        DataPipeline pipeline,
        ITextEmbeddingBatchGenerator generator,
        int batchSize,
        List<PartitionInfo> partitions,
        CancellationToken cancellationToken)
    {
        PartitionInfo[][] batches = partitions.Chunk(batchSize).ToArray();

        this._log.LogTrace("Generating embeddings, pipeline '{0}/{1}', batch generator '{2}', batch size {3}, batch count {4}",
            pipeline.Index, pipeline.DocumentId, generator.GetType().FullName, generator.MaxBatchSize, batches.Length);

        // One batch at a time
        foreach (PartitionInfo[] partitionsInfo in batches)
        {
            List<string> strings = new();
            foreach (var partition in partitionsInfo)
            {
                var textToEmbed = await _extractTextToEmbedAsync(partition.PartitionContent, cancellationToken).ConfigureAwait(false);
                strings.Add(textToEmbed);
            }

            int totalTokens = strings.Sum(s => ((ITextEmbeddingGenerator)generator).CountTokens(s));
            this._log.LogTrace("Generating embeddings, pipeline '{0}/{1}', generator '{2}', batch size {3}, total {4} tokens",
                pipeline.Index, pipeline.DocumentId, generator.GetType().FullName, strings.Count, totalTokens);

            Embedding[] embeddings = await generator.GenerateEmbeddingBatchAsync(strings, cancellationToken).ConfigureAwait(false);
            await this.SaveEmbeddingsToDocumentStorageAsync(
                    pipeline, partitionsInfo, embeddings, GetEmbeddingProviderName(generator), GetEmbeddingGeneratorName(generator), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // Generate and save embeddings, one chunk at a time
    private async Task GenerateEmbeddingsOneAtATimeAsync(
        DataPipeline pipeline,
        ITextEmbeddingGenerator generator,
        List<PartitionInfo> partitions,
        CancellationToken cancellationToken)
    {
        this._log.LogTrace("Generating embeddings, pipeline '{0}/{1}', generator '{2}', partition count {3}",
            pipeline.Index, pipeline.DocumentId, generator.GetType().FullName, partitions.Count);

        // One partition at a time
        foreach (PartitionInfo partitionInfo in partitions)
        {
            this._log.LogTrace("Generating embedding, pipeline '{0}/{1}', generator '{2}', content size {3} tokens",
                pipeline.Index, pipeline.DocumentId, generator.GetType().FullName, generator.CountTokens(partitionInfo.PartitionContent));

            //we need to transform the partition content
            var textToEmbed = await _extractTextToEmbedAsync(partitionInfo.PartitionContent, cancellationToken).ConfigureAwait(false);

            var embedding = await generator.GenerateEmbeddingAsync(textToEmbed, cancellationToken).ConfigureAwait(false);
            await this.SaveEmbeddingToDocumentStorageAsync(
                    pipeline, partitionInfo, embedding, GetEmbeddingProviderName(generator), GetEmbeddingGeneratorName(generator), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
