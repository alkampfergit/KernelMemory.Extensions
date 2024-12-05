using Moq;

namespace KernelMemory.Extensions.FunctionalTests.QueryPipeline;

public class ParallelQueryHandlerTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var handler = new ParallelQueryHandler("test");

        // Assert
        Assert.Equal("test", handler.Name);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleHandlers_CallsAllHandlers()
    {
        // Arrange
        var mock1 = new Mock<IQueryHandler>();
        var mock2 = new Mock<IQueryHandler>();
        var handler = new ParallelQueryHandler("test", mock1.Object, mock2.Object);
        var question = GetQuestion();

        // Act
        await handler.HandleAsync(question, CancellationToken.None);

        // Assert
        mock1.Verify(x => x.HandleAsync(question, CancellationToken.None), Times.Once);
        mock2.Verify(x => x.HandleAsync(question, CancellationToken.None), Times.Once);
    }

    private static UserQuestion GetQuestion()
    {
        return new UserQuestion(new UserQueryOptions("test index"), "question");
    }

    [Fact]
    public async Task HandleAsync_WithNoHandlers_CompletesSuccessfully()
    {
        // Arrange
        var handler = new ParallelQueryHandler("test");
        var question = GetQuestion();

        // Act & Assert
        await handler.HandleAsync(question, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_PropagatesException()
    {
        // Arrange
        var mock = new Mock<IQueryHandler>();
        mock.Setup(x => x.HandleAsync(It.IsAny<UserQuestion>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        var handler = new ParallelQueryHandler("test", mock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            handler.HandleAsync(GetQuestion(), CancellationToken.None));
    }
}
