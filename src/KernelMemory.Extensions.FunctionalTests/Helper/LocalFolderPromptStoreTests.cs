using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KernelMemory.Extensions.FunctionalTests.Helper;

public class LocalFolderPromptStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<ILogger<LocalFolderPromptStore>> _loggerMock;
    private readonly LocalFolderPromptStore _store;

    public LocalFolderPromptStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"promptstore_tests_{Guid.NewGuid()}");
        _loggerMock = new Mock<ILogger<LocalFolderPromptStore>>();
        _store = new LocalFolderPromptStore(_testDirectory, _loggerMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetPromptAsync_NonExistentKey_ReturnsNull()
    {
        // Act
        var result = await _store.GetPromptAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGetPromptAsync_ValidKey_ReturnsStoredPrompt()
    {
        // Arrange
        const string key = "test-key";
        const string expectedPrompt = "This is a test prompt";

        // Act
        await _store.SetPromptAsync(key, expectedPrompt);
        var result = await _store.GetPromptAsync(key);

        // Assert
        Assert.Equal(expectedPrompt, result);
    }

    [Fact]
    public async Task GetPromptAndSetDefaultAsync_NonExistentKey_SetsAndReturnsDefault()
    {
        // Arrange
        const string key = "default-key";
        const string defaultPrompt = "Default prompt value";

        // Act
        var result = await _store.GetPromptAndSetDefaultAsync(key, defaultPrompt);
        var storedPrompt = await _store.GetPromptAsync(key);

        // Assert
        Assert.Equal(defaultPrompt, result);
        Assert.Equal(defaultPrompt, storedPrompt);
    }

    [Fact]
    public async Task GetPromptAndSetDefaultAsync_ExistingKey_ReturnsExistingPrompt()
    {
        // Arrange
        const string key = "existing-key";
        const string existingPrompt = "Existing prompt";
        const string defaultPrompt = "Default prompt";
        await _store.SetPromptAsync(key, existingPrompt);

        // Act
        var result = await _store.GetPromptAndSetDefaultAsync(key, defaultPrompt);

        // Assert
        Assert.Equal(existingPrompt, result);
    }

    [Fact]
    public async Task SetPromptAsync_KeyWithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        const string key = "special/\\*:?\"<>|characters";
        const string expectedPrompt = "Prompt with special characters";

        // Act
        await _store.SetPromptAsync(key, expectedPrompt);
        var result = await _store.GetPromptAsync(key);

        // Assert
        Assert.Equal(expectedPrompt, result);
    }

    [Fact]
    public async Task GetPromptAndSetDefaultAsync_MissingPlaceholder_LogsError()
    {
        // Arrange
        const string key = "test-placeholder";
        const string existingPrompt = "A prompt without placeholder";
        const string defaultPrompt = "Default prompt with {{$placeholder}}";
        await _store.SetPromptAsync(key, existingPrompt);

        // Act
        var result = await _store.GetPromptAndSetDefaultAsync(key, defaultPrompt);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("{{$placeholder}}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Once);
    }

    [Fact]
    public async Task GetPromptAndSetDefaultAsync_MultipleMissingPlaceholders_LogsMultipleErrors()
    {
        // Arrange
        const string key = "test-multiple-placeholders";
        const string existingPrompt = "A prompt without any placeholders";
        const string defaultPrompt = "Default with {{$first}} and {{$second}}";
        await _store.SetPromptAsync(key, existingPrompt);

        // Act
        var result = await _store.GetPromptAndSetDefaultAsync(key, defaultPrompt);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetPromptAndSetDefaultAsync_ValidPlaceholders_NoErrors()
    {
        // Arrange
        const string key = "test-valid-placeholders";
        const string existingPrompt = "A prompt with {{$placeholder}} correctly set";
        const string defaultPrompt = "Default with {{$placeholder}}";
        await _store.SetPromptAsync(key, existingPrompt);

        // Act
        var result = await _store.GetPromptAndSetDefaultAsync(key, defaultPrompt);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Never);
    }
}
