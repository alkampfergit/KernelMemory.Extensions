using KernelMemory.Extensions.Helper;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using UglyToad.PdfPig.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;

namespace KernelMemory.Extensions.QueryPipeline;

/// <summary>
/// When we have conversation we need to be able to rewrite
/// the actual question to be a standalone question in the
/// context of that conversation. Usually this is done with an LLM
/// but it is clearly needed to use an interface for this.
/// </summary>
public interface IConversationQueryRewriter
{
    /// <summary>
    /// Rewrite the question in the context of the conversation.
    /// </summary>
    /// <param name="conversation">The actual conversation</param>
    /// <param name="question">The question to rewrite</param>
    /// <returns>The rewritten question, now that question is a standalone
    /// question that contains also the previous context.</returns>
    Task<string> RewriteAsync(Conversation conversation, string question, CancellationToken  cancellationToken = default);
}

public class SemanticKernelQueryRewriter : IConversationQueryRewriter
{
    private readonly SemanticKernelQueryRewriterOptions _semanticKernelQueryRewriterOptions;
    private readonly IPromptStore _promptStore;
    private readonly ISemanticKernelWrapper _kernel;
    private readonly ILogger<SemanticKernelQueryRewriter> _log;

    public SemanticKernelQueryRewriter(
        SemanticKernelQueryRewriterOptions semanticKernelQueryRewriterOptions,
        IPromptStore promptStore,
        ISemanticKernelWrapper kernel,
        ILogger<SemanticKernelQueryRewriter>? log = null)
    {
        _semanticKernelQueryRewriterOptions = semanticKernelQueryRewriterOptions;
        _promptStore = promptStore;
        _kernel = kernel;
        _log = log ?? DefaultLogger<SemanticKernelQueryRewriter>.Instance;
    }

    public async Task<string> RewriteAsync(Conversation conversation, string question, CancellationToken  cancellationToken = default)
    {
        var chatCompletionService = _kernel.GetChatCompletionService();

        ChatHistory chatMessages = new();

        foreach (var conversationQuestion in conversation.GetQuestions())
        {
            chatMessages.AddUserMessage(conversationQuestion.Question);
            if (conversationQuestion.Answered)
            {
                chatMessages.AddAssistantMessage(conversationQuestion.Answer!);
            }
            else
            {
                chatMessages.AddAssistantMessage("I do not know the answer");
            }
        }
        string defPrompt = $@"You will reformulate the question based on the conversation up to this point so the question will
be a standalone question that contains also the previous context. If there is no correlation
between the conversation and the question you will output the question unchanged.
You will answer only with the rewritten question no other text must be included.
Question: {question}";
        var prompt = await _promptStore.GetPromptAndSetDefaultAsync("SemanticKernelQueryRewriter", defPrompt, cancellationToken);
        chatMessages.AddUserMessage(prompt);

        var result = await chatCompletionService!.GetChatMessageContentAsync(
            chatMessages,
            executionSettings: new PromptExecutionSettings()
            {
                ModelId = _semanticKernelQueryRewriterOptions.ModelId
            },
            kernel: null,
            cancellationToken: cancellationToken
        );

        return result?.ToString() ?? question;
    }
}

/// <summary>
/// Allows some parametrization of the rewriter.
/// </summary>
public class SemanticKernelQueryRewriterOptions
{
    public string? ModelId { get; set; }

    public float Temperature { get; set; } = 0.0f;
}

public class HandlebarSemanticKernelQueryRewriter : IConversationQueryRewriter
{
    private const string DefaultPromptTemplate = @"system: 
* Given the following conversation history and the users next question, rephrase the 
follow up input to be a stand alone question.
If the conversation is irrelevant or empty, just restate the original question.
Do not add more details than necessary to the question.

chat history: 
{{#each history}}
question: 
{{question}}
answer: 
{{answer}}
{{/each}}

Follow up Input: {{ chat_input }} 
Standalone Question:";

    private readonly ConcurrentDictionary<string, KernelFunction> _functionCache = new();
    private readonly SemanticKernelQueryRewriterOptions _semanticKernelQueryRewriterOptions;
    private readonly ISemanticKernelWrapper _kernel;
    private readonly IPromptStore _promptStore;

    public HandlebarSemanticKernelQueryRewriter(
        SemanticKernelQueryRewriterOptions semanticKernelQueryRewriterOptions,
        ISemanticKernelWrapper kernel,
        IPromptStore promptStore)
    {
        _semanticKernelQueryRewriterOptions = semanticKernelQueryRewriterOptions;
        _kernel = kernel;
        _promptStore = promptStore;
    }

    private async Task<KernelFunction> CreateRewriteFunction(CancellationToken  cancellationToken = default)
    {
        var template = await _promptStore.GetPromptAndSetDefaultAsync("HandlebarSemanticKernelQueryRewriter", DefaultPromptTemplate, cancellationToken);
        
        return _functionCache.GetOrAdd(template, _ => _kernel.CreateFunctionFromPrompt(new PromptTemplateConfig()
        {
            Name = "TestRewrite",
            Description = "Rewrite a query for kernel memory.",
            Template = template,
            TemplateFormat = "handlebars",
            InputVariables =
            [
                new() { Name = "chat_input", Description = "New question of the user", IsRequired = false, Default = "" },
                new() { Name = "history", Description = "The history of the RAG CHAT.", IsRequired = true }
            ],
            ExecutionSettings =
            {
                { "default", new OpenAIPromptExecutionSettings()
                    {
                        MaxTokens = 1000,
                        Temperature = 0,
                        ModelId = "gpt35",
                    }
                },
            }
        },
        promptTemplateFactory: new HandlebarsPromptTemplateFactory()));
    }

    public async Task<string> RewriteAsync(Conversation conversation, string question, CancellationToken  cancellationToken = default)
    {
        KernelArguments ka = new();
        ka["chat_input"] = question;
        ka["history"] = conversation.GetQuestions();

        var chatFunction = await CreateRewriteFunction();
        var result = await _kernel.InvokeAsync("RewriteQuery", chatFunction, ka);

        return result?.ToString() ?? question;
    }
}
