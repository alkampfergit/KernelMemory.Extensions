using Microsoft.KernelMemory.Context;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Linq;

namespace KernelMemory.Extensions.Helper;

/// <summary>
/// A generic object that represent a call log to a Large Language Model.
/// </summary>
public class LLMCallLog
{
    public string CallName { get; set; } = null!;

    public string InputPrompt { get; set; } = null!;

    public string? Output { get; set; }

    public object? ReturnObject { get; set; }

    public TokenCount TokenCount { get; set; } = null!;

    public void AddOpenaiChatMessageContent(OpenAIChatMessageContent mc)
    {
        // check if the answer is a tool call or a standard Answer
        var isToolAnswer = mc.ToolCalls?.Any() == true;

        if (!isToolAnswer)
        {
            TextContent content = mc.Items.First() as TextContent;
            this.Output = content.Text;
        }
        else
        {
            var toolCall = mc.ToolCalls!.Single();

            this.Output = $"Function Call: Function {toolCall.FunctionName} with arguments {toolCall.FunctionArguments}";
        }

        // now token usage
        if (mc.Metadata?.TryGetValue("Usage", out var usage) == true && usage is ChatTokenUsage ctusage)
        {
            this.TokenCount = new TokenCount()
            {
                InputTokens = ctusage.InputTokenCount,
                OutputTokens = ctusage.OutputTokenCount,
                CachedTokenRead = ctusage.InputTokenDetails?.CachedTokenCount ?? 0,
            };
        }
    }
}

public class TokenCount
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedTokenRead { get; set; }

    public int CachedTokenWrite { get; set; }
}

/// <summary>
/// A collection of call log to a large language model.
/// </summary>
public class LLMCallLogContext
{
    public IReadOnlyList<LLMCallLog> CallLogs => _callLogs;

    private readonly List<LLMCallLog> _callLogs = new();

    public void AddCallLog(LLMCallLog callLog)
    {
        _callLogs.Add(callLog);
    }
}

public static class LLMCallLogExtensions
{
    public static LLMCallLogContext InitializeCallLogContext(this IContextProvider contextProvider)
    {
        LLMCallLogContext lLMCallLogContext = new();
        var context = contextProvider.GetContext();
        if (context != null)
        {
            context.Arguments[nameof(LLMCallLogContext)] = lLMCallLogContext;
        }
        return lLMCallLogContext;
    }

    public static LLMCallLogContext? GetCallLogContext(this IContextProvider contextProvider)
    {
        var context = contextProvider.GetContext();
        if (context != null)
        {
            if (context.Arguments.TryGetValue(nameof(LLMCallLogContext), out var llmCallLogContext))
            {
                return llmCallLogContext as LLMCallLogContext;
            }
        }
        return null;
    }

    public static void AddCallLog(this IContext context, LLMCallLog callLog)
    {
        if (!context.Arguments.TryGetValue(nameof(LLMCallLogContext), out var llmCallLogContext))
        {
            llmCallLogContext = new LLMCallLogContext();
            context.Arguments[nameof(LLMCallLogContext)] = llmCallLogContext;
        }

        ((LLMCallLogContext)llmCallLogContext).AddCallLog(callLog);
    }
}
