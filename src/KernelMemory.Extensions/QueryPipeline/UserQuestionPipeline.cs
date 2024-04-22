using KernelMemory.Extensions.QueryPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions
{
    public class UserQuestionPipeline
    {
        private readonly List<IQueryHandler> _queryHandlers = new List<IQueryHandler>();

        private IReRanker _reRanker = new BaseReRanker();

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
            userQuestion._reRanker = this._reRanker;
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

                HandleCitations(userQuestion);

                yield return new UserQuestionProgress(UserQuestionProgressType.PipelineCompleted, "Pipeline completed");
            }
        }

        public async Task ExecuteQuery(UserQuestion userQuestion, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userQuestion.Question))
            {
                return;
            }
            userQuestion._reRanker = this._reRanker;
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

            HandleCitations(userQuestion);
        }

        private static void HandleCitations(UserQuestion userQuestion)
        {
            //if the question was answered we need to group citations
            //TODO: change the internal data instead of using citations?
            if (userQuestion.Answered && userQuestion.Citations != null)
            {
                userQuestion.Citations = userQuestion
                    .Citations
                    .GroupBy(c => c.Link)
                    .Select(g =>
                    {
                        var firstCitation = g.First();
                        //Accumulate all other segments
                        firstCitation.Partitions = g.Select(c => c.Partitions.Single()).ToList();
                        return firstCitation;
                    }).ToList();
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
}
