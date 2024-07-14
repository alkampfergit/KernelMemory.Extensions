using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.QueryPipeline;

public class OpenAIRagQueryExecutorConfiguration
{
    /// <summary>
    /// This is the name of the OpenAI model used to 
    /// create the correct tokenizer. If you are using
    /// the default <see cref="ModelId"/> automatically
    /// we will use a standard gpt3.5 model
    /// </summary>
    public string ModelName { get; set; } = "gpt35";

    /// <summary>
    /// This is the modelId configured in Semantic Kernel 
    /// that helps using the correct model. If it is null
    /// we will use the default model
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Max tokens in the request, if not specified 3000 tokens
    /// will be used
    /// </summary>
    public int MaxTokens { get; set; } = 3000;

    /// <summary>
    /// Temperature of the request.
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// If GPT returns no citation we can remove the answer.
    /// </summary>
    public bool RemoveAnswerIfNoCitations { get; set; } = false;
}

/// <summary>
/// Executes the query part, it will start with a predefined prompt
/// and then add in the prompt all the retrieved memories as fact and
/// then use the LLM to answer user query with the fact (grounding).
/// </summary>
public class OpenaiRagQueryExecutor : BasicQueryHandler
{
    public override string Name => "OpenaiRagQueryExecutor";

    private readonly Kernel _kernel;
    private readonly OpenAIRagQueryExecutorConfiguration _config;
    private readonly Tokenizer _tokenizer;
    private readonly ILogger<StandardRagQueryExecutor> _log;

    public OpenaiRagQueryExecutor(
        Kernel kernel,
        OpenAIRagQueryExecutorConfiguration? config = null,
        ILogger<StandardRagQueryExecutor>? log = null)
    {
        _kernel = kernel;
        _config = config ?? new OpenAIRagQueryExecutorConfiguration();
        _tokenizer = Tiktoken.CreateTiktokenForModel(_config.ModelName);
        _log = log ?? DefaultLogger<StandardRagQueryExecutor>.Instance;
    }

    protected override async Task OnHandleAsync(
        UserQuestion userQuestion,
        CancellationToken cancellationToken)
    {
        var memoryRecords = await userQuestion.GetMemoryOrdered();
        if (memoryRecords.Count == 0)
        {
            //Well we have no memory we can simply return. 
            return;
        }

        //This code is taken and modified from the original KernelMemory codebase
        //Create a base of facts in a stringbuilder.
        var facts = new StringBuilder();

        //Then we need to stop adding facts when we reach the max number of tokens
        var tokensAvailable = _config.MaxTokens - _tokenizer.CountTokens(userQuestion.Question);

        //TODO: Add the preambole of the prompt to token count

        //Some statistics to tell how many facts we have available and how many we used.
        int factsAvailableCount = memoryRecords.Count;
        int factsUsedCount = 0;

        //we need to get the list of all memory record used, because we will need them 
        //to build citations.
        List<MemoryRecord> memoryRecordToUse = new();
        int docNumber = 0;
        foreach (var mr in memoryRecords)
        {
            factsAvailableCount++;
            var partitionText = mr.GetPartitionText();

            var size = _tokenizer.CountTokens(partitionText);
            if (size >= tokensAvailable)
            {
                // Stop after reaching the max number of tokens
                break;
            }

            factsUsedCount++;

            //Create a special format for the fact.
            var fact = $"---\nDocument {++docNumber}:\n{partitionText}\n";

            facts.Append(fact);
            memoryRecordToUse.Add(mr);
            tokensAvailable -= size;
        }

        if (factsAvailableCount > 0 && factsUsedCount == 0)
        {
            _log.LogError("Unable to inject memories in the prompt, not enough tokens available");
            return;
        }

        if (factsUsedCount == 0)
        {
            _log.LogWarning("No memories available");
            return;
        }
        var watch = new Stopwatch();
        watch.Restart();

        var openaiAnswer = await GenerateAnswerAsync(userQuestion.Question, facts.ToString(), cancellationToken);

        if (openaiAnswer == null)
        {
            //now answer is possible, then we can let the question flow to another handler.
            return;
        }

        //ok now we want to add answer and citations.
        watch.Stop();
        _log.LogTrace("Answer generated in {0} msecs", watch.ElapsedMilliseconds);

        userQuestion.Answer = openaiAnswer.Answer;

        List<MemoryRecord> usedMemoryRecord = memoryRecordToUse
            .Where((_, i) => openaiAnswer.Documents.Contains(i))
            .ToList();

        // now we need to clean up the citations, including only the one used to answer the question
        userQuestion.Citations = MemoryRecordHelper.BuildCitations(usedMemoryRecord, userQuestion.UserQueryOptions.Index, this._log);

        // ground if needed
        if (_config.RemoveAnswerIfNoCitations && userQuestion.Citations!.Count == 0)
        {
            //no answer is possible, because we do not have citations.
            userQuestion.Answer = null;
        }
    }

    private class GptAnswer
    {
        public string Answer { get; set; }

        public HashSet<int> Documents { get; set; }
    }

    /// <summary>
    /// Perform the call to the OpenAI API to get the answer to the question,
    /// it uses tool call to coerce the answer to not only return the answer text
    /// but also the array of documents that are used to answer the query
    /// </summary>
    /// <param name="question"></param>
    /// <param name="documents"></param>
    /// <returns></returns>
    private async Task<GptAnswer?> GenerateAnswerAsync(
        string question,
        string documents,
        CancellationToken token)
    {
        //First step is creating the function in Semantic Kernel.
        var function = KernelFunctionFactory.CreateFromMethod(
            [Description("Return the result to the user")] (
            [Description("Answer of the question")] string answer,
            [Description("Documents used to formulate the answer")] int[] documents
        ) =>
        {
        }, "return_result");
        var plugin = KernelPluginFactory.CreateFromFunctions("MyPlugin", [function]);
        var openAIFunction = plugin.GetFunctionsMetadata().First().ToOpenAIFunction();

        // Create a template for chat with settings
        var chat = _kernel.CreateFunctionFromPrompt(new PromptTemplateConfig()
        {
            Name = "Rag",
            Description = "Answer user question with documents.",
            Template = @"You are an AI assistant that helps users answer questions given a specific context. You will be given a context and asked a question based on that context. Your answer should be as precise as possible and should only come from the context.
Please add all documents used as citations.
Question: {{$question}}

Documents:
{{$documents}}",
            TemplateFormat = "semantic-kernel",
            InputVariables =
            [
                new() { Name = "question", Description = "Question of the user.", IsRequired = true },
                new() { Name = "documents", Description = "Documents needed to answer the query.", IsRequired = true }
            ],
            ExecutionSettings =
            {
                { "default", new OpenAIPromptExecutionSettings()
                    {
                        MaxTokens = 1000,
                        Temperature = 0,
                        ModelId = _config.ModelId,
                        ChatSystemPrompt = "You will answer question of the user using only documents in the prompt",
                        ToolCallBehavior = ToolCallBehavior.RequireFunction(openAIFunction, false),
                    }
                },
            }
        });

        KernelArguments ka = new();
        ka["question"] = question;
        ka["documents"] = documents;
        var result = await _kernel.InvokeAsync(chat, ka, token);

        var openaiMessageContent = result.GetValue<OpenAIChatMessageContent>();
        if (result is FunctionResult fre)
        {
            var toolCall = openaiMessageContent.GetOpenAIFunctionToolCalls().Single();
            var answer = ((JsonElement)toolCall.Arguments["answer"]).GetString()!;

            //-1 is because GPT is 1 based with document.
            var citations = ((JsonElement)toolCall.Arguments["documents"]).EnumerateArray().Select(e => e.GetInt32() - 1).ToHashSet();
            return new GptAnswer()
            {
                Answer = answer,
                Documents = citations
            };
        }

        return null;
    }
}
