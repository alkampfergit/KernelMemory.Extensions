using KernelMemory.Extensions.Helper;
using KernelMemory.Extensions.QueryPipeline;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace KernelMemory.Extensions.FunctionalTests.QueryPipeline;

public class SemanticKernelQueryRewriterTests
{
    private readonly Mock<IPromptStore> _promptStoreMock;
    private readonly Mock<ISemanticKernelWrapper> _kernelWrapperMock;
    private readonly Mock<IChatCompletionService> _chatCompletionServiceMock;
    private readonly SemanticKernelQueryRewriterOptions _options;

    public SemanticKernelQueryRewriterTests()
    {
        _promptStoreMock = new Mock<IPromptStore>();
        _kernelWrapperMock = new Mock<ISemanticKernelWrapper>();
        _chatCompletionServiceMock = new Mock<IChatCompletionService>();
        _options = new SemanticKernelQueryRewriterOptions { ModelId = "gpt-4" };

        _kernelWrapperMock.Setup(x => x.GetChatCompletionService())
            .Returns(_chatCompletionServiceMock.Object);
    }

    [Fact]
    public async Task RewriteAsync_ShouldUsePromptFromStore()
    {
        // Arrange
        var conversation = new Conversation();
        var question = "What is the weather?";
        var customPrompt = "Custom prompt template {{question}}";

        _promptStoreMock.Setup(x => x.GetPromptAndSetDefaultAsync(
            "SemanticKernelQueryRewriter",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(customPrompt);

        _chatCompletionServiceMock.Setup(x => x.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings?>(),
            It.IsAny<Kernel?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatMessageContent(AuthorRole.Assistant, "Rewritten question")]);

        var rewriter = new SemanticKernelQueryRewriter(_options, _promptStoreMock.Object, _kernelWrapperMock.Object);

        // Act
        var result = await rewriter.RewriteAsync(conversation, question);

        // Assert
        _promptStoreMock.Verify(x => x.GetPromptAndSetDefaultAsync(
            "SemanticKernelQueryRewriter",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal("Rewritten question", result);
    }

    /// <summary>
    /// IF we cannot rewrite, we cannot answer
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task RewriteAsync_WhenChatCompletionFails_ShouldThrow()
    {
        // Arrange
        var conversation = new Conversation();
        var question = "What is the weather?";

        _promptStoreMock.Setup(x => x.GetPromptAndSetDefaultAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("prompt");

        _chatCompletionServiceMock.Setup(x => x.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings?>(),
            It.IsAny<Kernel?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>());

        var rewriter = new SemanticKernelQueryRewriter(_options, _promptStoreMock.Object, _kernelWrapperMock.Object);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => rewriter.RewriteAsync(conversation, question));
    }
}
