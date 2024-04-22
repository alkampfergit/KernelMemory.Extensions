using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Prompts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions
{
    public class StandardRagQueryExecutor : BasicAsyncQueryHandlerWithProgress
    {
        public override string Name => "StandardRagQueryExecutor";

        private readonly string _answerPrompt;
        private readonly SearchClientConfig _config;
        private readonly ITextGenerator _textGenerator;
        private readonly ILogger<StandardRagQueryExecutor> _log;

        public StandardRagQueryExecutor(
            ITextGenerator textGenerator,
            SearchClientConfig? config = null,
            IPromptProvider? promptProvider = null,
            ILogger<StandardRagQueryExecutor>? log = null)
        {
            this._config = config ?? new SearchClientConfig();
            this._config.Validate();
            _textGenerator = textGenerator;
            this._log = log ?? DefaultLogger<StandardRagQueryExecutor>.Instance;
            promptProvider ??= new EmbeddedPromptProvider();
            this._answerPrompt = promptProvider.ReadPrompt(Constants.PromptNamesAnswerWithFacts);
        }

        protected override async IAsyncEnumerable<UserQuestionProgress> OnHandleStreamingAsync(
            UserQuestion userQuestion,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var citations = await userQuestion.GetAvailableCitationsAsync();
            if (citations.Count == 0)
            {
                //Well we have no memory we can simply return. 
                yield break;
            }

            var facts = new StringBuilder();
            var maxTokens = this._config.MaxAskPromptSize > 0
                ? this._config.MaxAskPromptSize
                : this._textGenerator.MaxTokenTotal;
            var tokensAvailable = maxTokens
                - this._textGenerator.CountTokens(this._answerPrompt)
                - this._textGenerator.CountTokens(userQuestion.Question)
                - this._config.AnswerTokens;

            int factsAvailableCount = citations.Count;
            int factsUsedCount = 0;
            List<Citation> usedCitations = new List<Citation>();
            foreach (var citation in citations)
            {
                factsAvailableCount++;
                var partition = citation.Partitions.Single();
                var fact = $"==== [File:{citation.SourceName};Relevance:{partition.Relevance:P1}]:\n{partition.Text}\n";

                var size = this._textGenerator.CountTokens(fact);
                if (size >= tokensAvailable)
                {
                    // Stop after reaching the max number of tokens
                    break;
                }

                factsUsedCount++;
                this._log.LogTrace("Adding text {0} with relevance {1}", factsUsedCount, partition.Relevance);

                facts.Append(fact);
                usedCitations.Add(citation);
                tokensAvailable -= size;
            }

            if (factsAvailableCount > 0 && factsUsedCount == 0)
            {
                this._log.LogError("Unable to inject memories in the prompt, not enough tokens available");
                yield break;
            }

            if (factsUsedCount == 0)
            {
                this._log.LogWarning("No memories available");
                yield break;
            }

            var text = new StringBuilder();
            var charsGenerated = 0;
            var watch = new Stopwatch();
            watch.Restart();
            await foreach (var x in this.GenerateAnswerAsync(userQuestion.Question, facts.ToString())
                               .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return new UserQuestionProgress(UserQuestionProgressType.AnswerPart, x);
                text.Append(x);

                if (this._log.IsEnabled(LogLevel.Trace) && text.Length - charsGenerated >= 30)
                {
                    charsGenerated = text.Length;
                    this._log.LogTrace("{0} chars generated", charsGenerated);
                }
            }

            watch.Stop();
            this._log.LogTrace("Answer generated in {0} msecs", watch.ElapsedMilliseconds);

            userQuestion.Answer = text.ToString();
            // now we need to clean up the citations, including only the one used to answer the question
            userQuestion.Citations = usedCitations.AsReadOnly();
        }

        /// <summary>
        /// Inner function is inerently async, so we can use the async enumerable
        /// </summary>
        /// <param name="question"></param>
        /// <param name="facts"></param>
        /// <returns></returns>
        private IAsyncEnumerable<string> GenerateAnswerAsync(string question, string facts)
        {
            var prompt = this._answerPrompt;
            prompt = prompt.Replace("{{$facts}}", facts.Trim(), StringComparison.OrdinalIgnoreCase);

            question = question.Trim();
            question = question.EndsWith('?') ? question : $"{question}?";
            prompt = prompt.Replace("{{$input}}", question, StringComparison.OrdinalIgnoreCase);

            prompt = prompt.Replace("{{$notFound}}", this._config.EmptyAnswer, StringComparison.OrdinalIgnoreCase);

            var options = new TextGenerationOptions
            {
                Temperature = this._config.Temperature,
                TopP = this._config.TopP,
                PresencePenalty = this._config.PresencePenalty,
                FrequencyPenalty = this._config.FrequencyPenalty,
                MaxTokens = this._config.AnswerTokens,
                StopSequences = this._config.StopSequences,
                TokenSelectionBiases = this._config.TokenSelectionBiases,
            };

            if (this._log.IsEnabled(LogLevel.Debug))
            {
                this._log.LogDebug("Running RAG prompt, size: {0} tokens, requesting max {1} tokens",
                    this._textGenerator.CountTokens(prompt),
                    this._config.AnswerTokens);
            }

            return this._textGenerator.GenerateTextAsync(prompt, options);
        }

        private static bool ValueIsEquivalentTo(string value, string target)
        {
            value = value.Trim().Trim('.', '"', '\'', '`', '~', '!', '?', '@', '#', '$', '%', '^', '+', '*', '_', '-', '=', '|', '\\', '/', '(', ')', '[', ']', '{', '}', '<', '>');
            target = target.Trim().Trim('.', '"', '\'', '`', '~', '!', '?', '@', '#', '$', '%', '^', '+', '*', '_', '-', '=', '|', '\\', '/', '(', ')', '[', ']', '{', '}', '<', '>');
            return string.Equals(value, target, StringComparison.OrdinalIgnoreCase);
        }
    }
}
