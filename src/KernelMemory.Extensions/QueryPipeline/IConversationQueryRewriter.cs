using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Threading.Tasks;

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
    Task<string> RewriteAsync(Conversation conversation, string question);
}

public class SemanticKernelQueryRewriter : IConversationQueryRewriter
{
    private readonly SemanticKernelQueryRewriterOptions _semanticKernelQueryRewriterOptions;
    private readonly Kernel _kernel;

    public SemanticKernelQueryRewriter(
        SemanticKernelQueryRewriterOptions semanticKernelQueryRewriterOptions,
        Kernel kernel)
    {
        _semanticKernelQueryRewriterOptions = semanticKernelQueryRewriterOptions;
        _kernel = kernel;
    }

    public async Task<string> RewriteAsync(Conversation conversation, string question)
    {
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

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
        string prompt = $@"You will reformulate the question based on the conversation up to this point so the question will
be a standalone question that contains also the previous context. If there is no correlation you will output the original question.
You will answer only with the new Question no other text must be included.
question {question}";
        chatMessages.AddUserMessage(prompt);

        var result = await chatCompletionService.GetChatMessageContentAsync(chatMessages, new PromptExecutionSettings()
        {
            ModelId = _semanticKernelQueryRewriterOptions.ModelId
        });

        return result?.ToString() ?? question;
    }

    /// <summary>
    /// Allows some parametrization of the rewriter.
    /// </summary>
    public class SemanticKernelQueryRewriterOptions
    {
        public string? ModelId { get; set; }

        public float Temperature { get; set; } = 0.0f;
    }
}
