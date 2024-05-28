using Azure;
using Azure.AI.OpenAI;
using KernelMemory.Extensions.Interfaces;
using Microsoft.KernelMemory;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Helper
{
    /// <summary>
    /// Used to create massive embedding vectors for a given text.
    /// </summary>
    public class AzureOpenaiEmbeddingGenerator : IBulkTextEmbeddingGenerator
    {
        private readonly OpenAIClient _client;
        private readonly MicrosoftMlTiktokenTokenizer _microsoftMlTiktokenTokenizer;
        private readonly string _deployment;
        private readonly int? _dimensions;

        public AzureOpenaiEmbeddingGenerator(
            AzureOpenAIConfig azureOpenAIConfig,
            MicrosoftMlTiktokenTokenizer microsoftMlTiktokenTokenizer)
        {
            _client = new OpenAIClient(new Uri(azureOpenAIConfig.Endpoint), new AzureKeyCredential(azureOpenAIConfig.APIKey));
            MaxTokens = azureOpenAIConfig.MaxTokenTotal;
            _microsoftMlTiktokenTokenizer = microsoftMlTiktokenTokenizer;
            _deployment = azureOpenAIConfig.Deployment;
            _dimensions = azureOpenAIConfig.EmbeddingDimensions;
        }

        public AzureOpenaiEmbeddingGenerator(
           AzureOpenAIConfig azureOpenAIConfig,
           string modelName) : this (azureOpenAIConfig, new MicrosoftMlTiktokenTokenizer(modelName))
        {
        }

        public int MaxTokens { get; }

        public int CountTokens(string text)
        {
            return _microsoftMlTiktokenTokenizer.CountTokens(text);
        }

        public async Task<Embedding> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var result = await GenerateEmbeddingsAsync([text], cancellationToken);
            return result[0];
        }

        public async Task<Embedding[]> GenerateEmbeddingsAsync(string[] text, CancellationToken cancellationToken = default)
        {
            var options = new EmbeddingsOptions(_deployment, text);
            if (_dimensions.HasValue)
            {
                options.Dimensions = _dimensions.Value;
            }
            var result = await _client.GetEmbeddingsAsync(options, cancellationToken);
            return result.Value.Data.Select(ei => new Embedding(ei.Embedding)).ToArray();
        }
    }
}
