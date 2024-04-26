using System.Runtime.CompilerServices;

namespace KernelMemory.Extensions.FunctionalTests.QueryPipeline;

public class AsyncUserQuestionPipelineTests
{
    [Fact]
    public async Task Can_support_Streaming()
    {
        var sut = GenerateSut();
        var options = GenerateOptions();
        var userQuestion = new UserQuestion(options, "test");

        sut.AddHandler(new SimpleTextGeneratorAsync());

        var enumerator = sut.ExecuteQueryAsync(userQuestion);
        var notifications = await enumerator.ToListAsync();

        //Question should be answered
        Assert.True(userQuestion.Answered);
        Assert.Equal("BLABLA Finished", userQuestion.Answer);

        //We should have received the notifications
        Assert.Equal(3, notifications.Count);
        Assert.Equal("BLABLA", notifications[0].Text);
        Assert.Equal(UserQuestionProgressType.AnswerPart, notifications[0].Type);
        Assert.Equal("Finished", notifications[1].Text);
        Assert.Equal(UserQuestionProgressType.AnswerPart, notifications[1].Type);
        Assert.Equal(UserQuestionProgressType.PipelineCompleted, notifications[2].Type);
    }

    private UserQueryOptions GenerateOptions()
    {
        return new UserQueryOptions("index");
    }

    private static UserQuestionPipeline GenerateSut()
    {
        return new UserQuestionPipeline();
    }

    private class SimpleTextGeneratorAsync : BasicAsyncQueryHandlerWithProgress
    {
        public override string Name => nameof(SimpleTextGeneratorAsync);

        protected override async IAsyncEnumerable<UserQuestionProgress> OnHandleStreamingAsync(UserQuestion userQuestion, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            //simulate something
            yield return new UserQuestionProgress(UserQuestionProgressType.AnswerPart, "BLABLA");
            await Task.Delay(1);
            userQuestion.Answer = "BLABLA Finished";
            yield return new UserQuestionProgress(UserQuestionProgressType.AnswerPart, "Finished");
        }
    }
}