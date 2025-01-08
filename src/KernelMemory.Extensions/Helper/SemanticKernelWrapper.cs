using Microsoft.KernelMemory.Context;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions.Helper;

/// <summary>
/// Abstract accessing Semantic Kernel to test and to intercept and perform some
/// operations like better logging.
/// </summary>
public interface ISemanticKernelWrapper
{
    KernelFunction CreateFunctionFromMethod(Delegate method, string functionName);

    KernelPlugin CreateFromFunctions(string pluginName, IEnumerable<KernelFunction> functions);

    KernelFunction CreateFunctionFromPrompt(PromptTemplateConfig config, IPromptTemplateFactory? promptTemplateFactory = null);

    /// <summary>
    /// Wrap the invocation of a kernel memory function, needed to intercept the call and log it.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="function"></param>
    /// <param name="args"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<FunctionResult> InvokeAsync(string name, KernelFunction function, KernelArguments? args = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wrapper to take the chat completion service.
    /// </summary>
    /// <returns></returns>
    IChatCompletionService GetChatCompletionService();
}

public class SemanticKernelWrapper : ISemanticKernelWrapper
{
    private readonly Kernel _kernel;
    private readonly IContextProvider _contextProvider;

    public SemanticKernelWrapper(Kernel kernel, IContextProvider contextProvider)
    {
        _kernel = kernel;
        _contextProvider = contextProvider;
    }

    public KernelFunction CreateFunctionFromMethod(Delegate method, string functionName)
    {
        return KernelFunctionFactory.CreateFromMethod(method, functionName);
    }

    public KernelPlugin CreateFromFunctions(string pluginName, IEnumerable<KernelFunction> functions)
    {
        return KernelPluginFactory.CreateFromFunctions(pluginName, functions);
    }

    public KernelFunction CreateFunctionFromPrompt(PromptTemplateConfig config, IPromptTemplateFactory? promptTemplateFactory = null)
    {
        return _kernel.CreateFunctionFromPrompt(config, promptTemplateFactory);
    }

    public async Task<FunctionResult> InvokeAsync(
        string name,
        KernelFunction function,
        KernelArguments? args = null,
        CancellationToken cancellationToken = default)
    {
        var retValue = await _kernel.InvokeAsync(function, args, cancellationToken);

        AddLogCall(name, retValue);

        return retValue;
    }

    private void AddLogCall(string name, FunctionResult retValue)
    {
        try
        {
            //Since we invoked a function we expect the call to be logged.
            var context = _contextProvider.GetContext();
            LLMCallLog callLog = new LLMCallLog
            {
                CallName = name,
                ReturnObject = retValue,
                InputPrompt = retValue.RenderedPrompt ?? "",
            };

            // Now we need to extract the real result call
            var mc = retValue.GetValue<OpenAIChatMessageContent>()!;

            callLog.AddOpenaiChatMessageContent(mc);

            context.AddCallLog(callLog);
        }
        catch (Exception)
        {
            //it is a log call so we do not want to throw an exception
        }
    }

    public IChatCompletionService GetChatCompletionService()
    {
        var ccs = _kernel.GetRequiredService<IChatCompletionService>();
        return new LogChatCompletionService(_contextProvider, ccs);
    }

    private class LogChatCompletionService : IChatCompletionService
    {
        private readonly IContextProvider _contextProvider;
        private readonly IChatCompletionService _ccs;
        public LogChatCompletionService(
            IContextProvider contextProvider,
            IChatCompletionService ccs)
        {
            _contextProvider = contextProvider;
            _ccs = ccs;
        }

        public IReadOnlyDictionary<string, object?> Attributes => _ccs.Attributes;

        public async Task<IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _ccs.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);

            var context = _contextProvider.GetContext();
            StringBuilder prompt = new();
            foreach (var chatMessage in chatHistory)
            {
                prompt.AppendLine($"\nRole: {chatMessage.Role}\n Message: {chatMessage.Content}\n");
            }
            LLMCallLog callLog = new LLMCallLog
            {
                CallName = "ChatCompletionService",
                ReturnObject = result,
                InputPrompt = prompt.ToString()
            };

            var answers = result.ToArray();
            foreach (var answer in answers)
            {
                if (answer is OpenAIChatMessageContent content)
                {
                    callLog.AddOpenaiChatMessageContent(content);
                }
            }

            context.AddCallLog(callLog);
            return result;
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            return _ccs.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        }
    }
}
