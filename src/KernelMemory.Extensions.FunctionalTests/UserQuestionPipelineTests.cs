using Microsoft.KernelMemory;
using Moq;

namespace KernelMemory.Extensions.FunctionalTests;

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
        Assert.Equal("expanded question", userQuestion.ExpandedQuestions.First().Text);
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

        Assert.Null(userQuestion.Answer);
        //Verify that we have extracted memories
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

        var citations1 = new Citation[]
        {
            CreateCitation("Document_1", "fileId", "lin1", "pieceoftextaa"),
            CreateCitation("Document_2", "fileId", "lin2", "pieceoftext3")
        };

        var citations2 = new Citation[]
        {
            CreateCitation("Document_3", "fileId", "lin1", "pieceoftext of the same file")
        };
        Mock<IQueryHandler> mockDependency1 = GenerateCitationsMock("citations1", citations1);
        Mock<IQueryHandler> mockDependency2 = GenerateCitationsMock("citations2", citations2);
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
    public async Task Standard_search_regroup_citations()
    {
        var sut = GenerateSut();
        var citations = new List<Citation>();
        citations.Add(CreateCitation("Document_1", "fileId", "lin1", "pieceoftextaa"));
        citations.Add(CreateCitation("Document_2", "fileId", "lin2", "pieceoftext3"));
        citations.Add(CreateCitation("Document_1", "fileId", "lin1", "pieceoftext of the same file"));

        Mock<IQueryHandler> citationMock = GenerateCitationsMock("test1", citations);
        Mock<IQueryHandler> answerMock = GenerateQueryAnswerMock("answered");

        sut.AddHandler(citationMock.Object);
        sut.AddHandler(answerMock.Object);

        var userQuestion = new UserQuestion(GenerateOptions(), "test question");
        await sut.ExecuteQuery(userQuestion);

        Assert.True(userQuestion.Answered);

        //now we need to verify the citations,
        Assert.Single(userQuestion.SourceCitations);
        Assert.Equal(3, userQuestion.SourceCitations["test1"].Count);

        var firstCitation1 = userQuestion.Citations.Single(c => c.Link == "lin1");
        var firstCitation2 = userQuestion.Citations.Single(c => c.Link == "lin2");

        Assert.Equal(2, firstCitation1.Partitions.Count);
        Assert.Single(firstCitation2.Partitions);

        Assert.Equal("pieceoftextaa", firstCitation1.Partitions[0].Text);
        Assert.Equal("pieceoftext of the same file", firstCitation1.Partitions[1].Text);
        Assert.Equal("pieceoftext3", firstCitation2.Partitions[0].Text);
    }

    private static Citation CreateCitation(string documentId, string fileId, string link, string textPartition)
    {
        return new Citation()
        {
            DocumentId = documentId,
            FileId = fileId,
            Link = link,
            Partitions = new List<Citation.Partition>()
            {
                new Citation.Partition()
                {
                    Text = textPartition
                }
            }
        };
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
                var citations = await x.GetAvailableCitationsAsync();
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
                var citations = await x.GetAvailableCitationsAsync();
                x.Citations = citations;
                x.Answer = answer;
            });

        //setup return value for the Name property
        mockDependency.Setup(x => x.Name).Returns(AnswerHandlerValue);

        return mockDependency;
    }

    private static Mock<IQueryHandler> GenerateCitationsMock(string sourceName, IEnumerable<Citation> citations)
    {
        //now I need to mock the HandleAsync method modifying the user question adding extra questions
        var mockDependency = new Mock<IQueryHandler>();
        mockDependency.Setup(x => x.HandleAsync(It.IsAny<UserQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<UserQuestion, CancellationToken>((x, _) => x.AddCitations(sourceName, citations));

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
            await userQuestion.GetAvailableCitationsAsync();

            userQuestion.Answer = "ANSWER";

            userQuestion.Citations = [CreateCitation("a", "b", "c", "d")];
        }
    }
}