using KernelMemory.Extensions.FunctionalTests.TestUtilities;
using KernelMemory.Extensions.QueryPipeline;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using Moq;

namespace KernelMemory.Extensions.FunctionalTests.QueryPipeline;

public class UserQuestionPipelineTests
{
    private const string AnswerHandlerValue = "AnswerHandler";

    [Fact]
    public async Task Null_query_has_no_answer()
    {
        var sut = GenerateSut();

        Mock<IQueryHandler> mockDependency = GenerateQueryExpanderMock("expanded question");
        Mock<IQueryHandler> answerMock = GenerateQueryAnswerMock("ANSWER");
        sut.AddHandler(mockDependency.Object);
        sut.AddHandler(answerMock.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "");
        await sut.ExecuteQuery(userQuestion);

        //now I need to assert that extended question was added
        Assert.False(userQuestion.Answered);
    }

    [Fact]
    public async Task Verify_basic_pipeline_generation()
    {
        var sut = GenerateSut();

        Mock<IQueryHandler> mockDependency = GenerateQueryExpanderMock("expanded question");
        sut.AddHandler(mockDependency.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        //now I need to assert that extended question was added
        Assert.Single(userQuestion.ExpandedQuestions);
        Assert.Equal("expanded question", userQuestion.ExpandedQuestions[0].Text);
    }

    [Fact]
    public async Task Verify_capability_of_execution_handler_to_answer()
    {
        var sut = GenerateSut();

        Mock<IQueryHandler> mockDependency = GenerateQueryExpanderMock("expanded question");
        Mock<IQueryHandler> answerMock = GenerateQueryAnswerMock("ANSWER");
        sut.AddHandler(mockDependency.Object);
        sut.AddHandler(answerMock.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        //now I need to assert that extended question was added
        Assert.Equal("ANSWER", userQuestion.Answer);
    }

    [Fact]
    public async Task When_an_handler_terminate_subsequent_handlers_are_not_called()
    {
        var sut = GenerateSut();

        Mock<IQueryHandler> mockDependency1 = GenerateQueryExpanderMock("expanded question1");
        Mock<IQueryHandler> mockDependency2 = GenerateQueryExpanderMock("expanded question2");
        Mock<IQueryHandler> answerMock = GenerateQueryAnswerMock("ANSWER");

        Assert.Equal("AnswerHandler", answerMock.Object.Name);

        sut.AddHandler(mockDependency1.Object);
        sut.AddHandler(answerMock.Object);
        sut.AddHandler(mockDependency2.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        //now I need to assert that extended question was added
        Assert.Equal("ANSWER", userQuestion.Answer);
        Assert.Single(userQuestion.ExpandedQuestions);
        Assert.Equal("expanded question1", userQuestion.ExpandedQuestions[0].Text);

        Assert.Equal(AnswerHandlerValue, userQuestion.AnswerHandler);
    }

    [Fact]
    public async Task When_no_handler_answered_the_question_the_answer_is_null()
    {
        var sut = GenerateSut();

        Mock<IQueryHandler> mockDependency1 = GenerateQueryExpanderMock("expanded question1");
        Mock<IQueryHandler> mockDependency2 = GenerateQueryExpanderMock("expanded question2");

        sut.AddHandler(mockDependency1.Object);
        sut.AddHandler(mockDependency2.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        //now I need to assert that extended question was added
        Assert.Null(userQuestion.Answer);
        Assert.False(userQuestion.Answered);
    }

    [Fact]
    public async Task Ability_to_extract_memory_segments_with_handlers()
    {
        var sut = GenerateSut();

        Mock<IQueryHandler> mockDependency1 = GenerateQueryExpanderMock("expanded question1");
        Mock<IQueryHandler> mockDependency2 = GenerateRetrievalMock("pieceoftext", "Document_1", "fileId");

        sut.AddHandler(mockDependency1.Object);
        sut.AddHandler(mockDependency2.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        Assert.NotNull(userQuestion);
        Assert.Null(userQuestion.Answer);

        //Verify that we have extracted memories
        Assert.NotNull(userQuestion.Citations);
        Assert.Single(userQuestion.Citations);

        var citation = userQuestion.Citations.First();
        Assert.Equal("pieceoftext", citation.Partitions.Single().Text);
        Assert.Equal("Document_1", citation.DocumentId);
        Assert.Equal("fileId", citation.FileId);
    }

    [Fact]
    public async Task Ability_to_re_rank()
    {
        var sut = GenerateSut();

        var memorySet1 = new MemoryRecord[]
        {
            MemoryRecordTestUtilities.CreateMemoryRecord("Document_1", "fileId", 1, "pieceoftextaa"),
            MemoryRecordTestUtilities.CreateMemoryRecord("Document_2", "fileId", 2, "pieceoftext3")
        };

        var memorySet2 = new MemoryRecord[]
        {
            MemoryRecordTestUtilities.CreateMemoryRecord("Document_3", "fileId", 1, "pieceoftext of the same file")
        };
        Mock<IQueryHandler> mockDependency1 = GenerateCitationsMock("citations1", memorySet1);
        Mock<IQueryHandler> mockDependency2 = GenerateCitationsMock("citations2", memorySet2);
        Mock<IQueryHandler> mockDependency3 = GenerateRetrievalMock("pieceoftext", "Document_1", "fileId");

        sut.AddHandler(mockDependency1.Object);
        sut.AddHandler(mockDependency2.Object);
        sut.AddHandler(mockDependency3.Object);
        sut.AddHandler(new BaseAnswerSimulator());

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        //We have no result because the pipeline went in error
        Assert.True(userQuestion.Answered);
        Assert.NotNull(userQuestion.Citations);
    }

    [Fact]
    public async Task Basic_conversation_handling()
    {
        var sut = GenerateSut();
        sut.AddHandler(new BaseAnswerSimulator());

        //Generate mock for conversation rewriter
        var conversationRewriterMock = new Mock<IConversationQueryRewriter>();
        conversationRewriterMock.Setup(x => x.RewriteAsync(It.IsAny<Conversation>(), It.IsAny<string>()))
            .Returns(Task.FromResult("New rewritten question"));
        sut.SetConversationQueryRewriter(conversationRewriterMock.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        var conversation = new Conversation();
        conversation.AddQuestion("Color of sky", "green");
        userQuestion.SetConversation(conversation);

        //Act: execute query we need to rewrite the query
        await sut.ExecuteQuery(userQuestion);

        //Assert: query was rewritten
        Assert.Equal("New rewritten question", userQuestion.Question);
    }

    [Fact]
    public async Task Basic_conversation_handling_async()
    {
        var sut = GenerateSut();
        sut.AddHandler(new BaseAnswerSimulator());

        //Generate mock for conversation rewriter
        var conversationRewriterMock = new Mock<IConversationQueryRewriter>();
        conversationRewriterMock.Setup(x => x.RewriteAsync(It.IsAny<Conversation>(), It.IsAny<string>()))
            .Returns(Task.FromResult("New rewritten question"));
        sut.SetConversationQueryRewriter(conversationRewriterMock.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        var conversation = new Conversation();
        conversation.AddQuestion("Color of sky", "green");
        userQuestion.SetConversation(conversation);

        //Act: execute query we need to rewrite the query
        var result = sut.ExecuteQueryAsync(userQuestion);
        await result.ToListAsync();

        //Assert: query was rewritten
        Assert.Equal("New rewritten question", userQuestion.Question);
    }

    /// <summary>
    /// This test was not really useful anymore after migration to a different 
    /// structure of citations.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Standard_search_regroup_citations()
    {
        var sut = GenerateSut();
        var citations = new List<MemoryRecord>();
        citations.Add(MemoryRecordTestUtilities.CreateMemoryRecord("Document_1", "fileId", 1, "pieceoftextaa"));
        citations.Add(MemoryRecordTestUtilities.CreateMemoryRecord("Document_2", "fileId", 2, "pieceoftext3"));
        citations.Add(MemoryRecordTestUtilities.CreateMemoryRecord("Document_1", "fileId", 1, "pieceoftext of the same file"));

        Mock<IQueryHandler> citationMock = GenerateCitationsMock("test1", citations);
        Mock<IQueryHandler> answerMock = GenerateQueryAnswerMock("answered");

        sut.AddHandler(citationMock.Object);
        sut.AddHandler(answerMock.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        Assert.True(userQuestion.Answered);

        //now we need to verify the citations,
        Assert.Single(userQuestion.MemoryRecordPool);
        Assert.Equal(3, userQuestion.MemoryRecordPool["test1"].Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task UserQuestion_re_rank(int numberOfSources) 
    {
        //Create a mock of iReRanker
        var mockReRanker = new Mock<IReRanker>();

        var sut = new UserQuestion(GenerateOptions(), "test");
        sut.AddReRanker(mockReRanker.Object);

        //add sources
        for (int i = 0; i < numberOfSources; i++)
        {
            sut.AddMemoryRecordSource($"test{i}", []);
        }

        await sut.GetMemoryOrdered();

        //Verify no call to the reranker is done.
        mockReRanker.Verify(x => x.ReRankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyCollection<MemoryRecord>>>()), Times.Never);
    }

    private UserQueryOptions GenerateOptions()
    {
        return new UserQueryOptions("index");
    }

    private static Mock<IQueryHandler> GenerateQueryExpanderMock(params string[] expandedQueryTests)
    {
        //now I need to mock the HandleAsync method modifying the user question adding extra questions
        var mockDependency = new Mock<IQueryHandler>();
        mockDependency.Setup(x => x.HandleAsync(It.IsAny<UserQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<UserQuestion, CancellationToken>((x, _) => x.ExpandedQuestions.AddRange(expandedQueryTests.Select(q => new Question(q))));
        return mockDependency;
    }

    private static Mock<IQueryHandler> GenerateRetrievalMock(string memoryText, string documetnId, string fileId)
    {
        //now I need to mock the HandleAsync method modifying the user question adding extra questions
        var mockDependency = new Mock<IQueryHandler>();
        mockDependency.Setup(x => x.HandleAsync(It.IsAny<UserQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<UserQuestion, CancellationToken>(async (x, _) =>
            {
                //It must simulate taking all the citations
                var citations = await x.GetMemoryOrdered();
                var citation = new Citation()
                {
                    DocumentId = documetnId,
                    FileId = fileId,
                };
                citation.Partitions = new List<Citation.Partition>()
                {
                    new Citation.Partition()
                    {
                        Text = memoryText
                    }
                };

                x.Citations = [citation];
            });

        //setup return value for the Name property
        mockDependency.Setup(x => x.Name).Returns(AnswerHandlerValue);

        return mockDependency;
    }

    private static Mock<IQueryHandler> GenerateQueryAnswerMock(string answer)
    {
        //now I need to mock the HandleAsync method modifying the user question adding extra questions
        var mockDependency = new Mock<IQueryHandler>();
        mockDependency.Setup(x => x.HandleAsync(It.IsAny<UserQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<UserQuestion, CancellationToken>(async (x, _) =>
            {
                //this simulate also a reranker.
                var memoryRecords = await x.GetMemoryOrdered();

                x.Citations = MemoryRecordHelper.BuildCitations(memoryRecords.ToList(), "test-index", NullLogger.Instance);
                x.Answer = answer;
            });

        //setup return value for the Name property
        mockDependency.Setup(x => x.Name).Returns(AnswerHandlerValue);

        return mockDependency;
    }

    private static Mock<IQueryHandler> GenerateCitationsMock(string sourceName, IEnumerable<MemoryRecord> memoryRecords)
    {
        //now I need to mock the HandleAsync method modifying the user question adding extra questions
        var mockDependency = new Mock<IQueryHandler>();
        mockDependency.Setup(x => x.HandleAsync(It.IsAny<UserQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<UserQuestion, CancellationToken>((x, _) => x.AddMemoryRecordSource(sourceName, memoryRecords));

        //setup return value for the Name property
        mockDependency.Setup(x => x.Name).Returns(AnswerHandlerValue);

        return mockDependency;
    }

    private static UserQuestionPipeline GenerateSut()
    {
        return new UserQuestionPipeline();
    }

    private class BaseAnswerSimulator : BasicQueryHandler
    {
        public BaseAnswerSimulator()
        {

        }

        public override string Name => nameof(BaseAnswerSimulator);

        protected override async Task OnHandleAsync(UserQuestion userQuestion, CancellationToken cancellationToken)
        {
            await userQuestion.GetMemoryOrdered();

            userQuestion.Answer = "ANSWER";

            var mr = new List<MemoryRecord>() { MemoryRecordTestUtilities.CreateMemoryRecord("a", "b", 1, "d") };
            userQuestion.Citations = MemoryRecordHelper.BuildCitations(mr, "test-index", NullLogger.Instance);
        }
    }
}