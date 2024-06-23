using KernelMemory.Extensions.QueryPipeline;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

public class UserQuestionPipeline
{
    private readonly List<IQueryHandler> _queryHandlers = new List<IQueryHandler>();

    private IReRanker _reRanker = new BaseReRanker();
    private IConversationQueryRewriter? _conversationQueryRewriter;

    public UserQuestionPipeline AddHandler(IQueryHandler queryHandler)
    {
        _queryHandlers.Add(queryHandler);
        return this;
    }

    public UserQuestionPipeline SetReRanker(IReRanker reRanker)
    {
        _reRanker = reRanker;
        return this;
    }

    public UserQuestionPipeline SetConversationQueryRewriter(IConversationQueryRewriter conversationQueryRewriter)
    {
        _conversationQueryRewriter = conversationQueryRewriter;
        return this;
    }

    public Task ExecuteQuery(UserQuestion userQuestion)
    {
        return ExecuteQuery(userQuestion, CancellationToken.None);
    }

    /// <summary>
    /// If we uses streaming api we can have a situation where we can get the answer
    /// a piece at a time, this is useful to keep the user engaged.
    /// </summary>
    /// <param name="userQuestion"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<UserQuestionProgress> ExecuteQueryAsync(UserQuestion userQuestion, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await PreProcessQuestion(userQuestion);

        //this is a completely different way to interact with the question, each handler should implement
        //the IAsyncEnumerable interface to communicate progress.
        if (!string.IsNullOrWhiteSpace(userQuestion.Question))
        {
            foreach (var handler in _queryHandlers)
            {
                //here is the different part, we need to understand if it is a streaming handler
                if (handler is IQueryHandlerWithProgress asyncQueryHandler)
                {
                    //Simply enumerate and return enumeration to the caller.
                    var progress = asyncQueryHandler.HandleStreamingAsync(userQuestion, cancellationToken);
                    await foreach (var item in progress)
                    {
                        yield return item;
                    }
                }
                else
                {
                    //The handler is a normal handler, we just need to call it
                    await handler.HandleAsync(userQuestion, cancellationToken);
                }

                if (userQuestion.Answered)
                {
                    //We break the pipeline if the question has been answered
                    userQuestion.AnswerHandler = handler.Name;
                    break;
                }
            }

            yield return new UserQuestionProgress(UserQuestionProgressType.PipelineCompleted, "Pipeline completed");
        }
    }

    public async Task ExecuteQuery(UserQuestion userQuestion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userQuestion.Question))
        {
            return;
        }

        await PreProcessQuestion(userQuestion);

        foreach (var handler in _queryHandlers)
        {
            //Execute the handler and verify if the question has been answered.
            try
            {
                await handler.HandleAsync(userQuestion, cancellationToken);
            }
            catch (Exception ex)
            {
                userQuestion.Errors = $"Exception in handler {handler.GetType().FullName} - {ex}";
                break;
            }

            if (userQuestion.Answered)
            {
                //We break the pipeline if the question has been answered
                userQuestion.AnswerHandler = handler.Name;
                break;
            }
        }
    }

    private async Task PreProcessQuestion(UserQuestion userQuestion)
    {
        userQuestion.ReRanker = this._reRanker;

        //Verify if there is an active conversation
        if (userQuestion.Conversation != null && _conversationQueryRewriter != null)
        {
            //Rewrite the question in the context of the conversation
            var newQuestion = await _conversationQueryRewriter.RewriteAsync(userQuestion.Conversation, userQuestion.Question);
            userQuestion.RewriteQuestion(newQuestion);
        }
    }
}

public record UserQuestionProgress(UserQuestionProgressType Type, string Text);

public enum UserQuestionProgressType
{
    Unknown = 0,
    Searching = 1,
    ReRanking = 2,
    AnswerPart = 3,
    PipelineCompleted = 4,
}
