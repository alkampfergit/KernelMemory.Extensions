using KernelMemory.Extensions.QueryPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Fasterflect;
using KernelMemory.Extensions.Helper;

namespace KernelMemory.Extensions.FunctionalTests.Helper;

public class OpenaiRagQueryExecutorTests
{
    private readonly OpenaiRagQueryExecutor _sut;
    private readonly Mock<IPromptStore> _mockPromptStore;
    private readonly Mock<ILogger<StandardRagQueryExecutor>> _mockLogger;
    private readonly Mock<ISemanticKernelWrapper> _mockKernel;

    public OpenaiRagQueryExecutorTests()
    {
        var kernel = new Kernel();
        _mockPromptStore = new Mock<IPromptStore>();
        _mockLogger = new Mock<ILogger<StandardRagQueryExecutor>>();
        _mockKernel = new Mock<ISemanticKernelWrapper>();
        _sut = new OpenaiRagQueryExecutor(_mockKernel.Object, new OpenAIRagQueryExecutorConfiguration(), _mockLogger.Object, _mockPromptStore.Object);
    }

    [Fact]
    public async Task GetPromptAsync_ShouldReturnPromptFromMock()
    {
        // Arrange
        var expectedPrompt = "Test Prompt";
        _mockPromptStore.Setup(store => store.GetPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expectedPrompt);

        // Act
        var task = (Task<string>)_sut.CallMethod("GetPromptAsync");
        var actualPrompt = await task;

        // Assert
        Assert.Equal(expectedPrompt, actualPrompt);
    }

    [Fact]
    public async Task GetPromptAsync_Should_validate_log_called()
    {
        // Arrange
        var invalidPrompt = "Invalid Prompt";
        _mockPromptStore.Setup(store => store.GetPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(invalidPrompt);

        // Act
        var task = (Task<string>)_sut.CallMethod("GetPromptAsync");
        var actualPrompt = await task;

        // Assert
        // template is not valid, we expect two log error
        // void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, type) => value.ToString()!.Contains("{{$question}}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, type) => value.ToString()!.Contains("{{$documents}}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

         // verify that store method of Ipromptstore is not called
        _mockPromptStore.Verify(
            store => store.SetPromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task If_prompt_not_saved_reload()
    {
        // Arrange
        _mockPromptStore.Setup(store => store.GetPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((String?) null); 

        // Act
        var task = (Task<string>)_sut.CallMethod("GetPromptAsync");
        var actualPrompt = await task;

        // Assert
        // verify that store method of Ipromptstore is called
        _mockPromptStore.Verify(
            store => store.SetPromptAsync("OpenaiRagQueryExecutor", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
