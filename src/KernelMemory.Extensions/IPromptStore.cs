using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

/// <summary>
/// <para>
/// To let the user to change prompt used in the various part of extensions
/// we introduce an interface that is capable of providing user prompt given a key.
/// The user can implement its own implementation of this interface and pass it to the extension.
/// </para>
/// <para>
/// We do not use propmtp provider from kernel memory because we need to have a way to set
/// the prompt into the provider for a better experience of the user
/// </para>
/// </summary>
public interface IPromptStore
{
    /// <summary>
    /// Get the prompt for the given key.
    /// </summary>
    /// <param name="key">The key for which the prompt is requested.</param>
    /// <returns>The prompt for the given key or null if the prompt is not present. If null is returned the 
    /// various components will use some default prompts.</returns>
    Task<string?> GetPromptAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the prompt for the given key and set the default prompt if the prompt is not present.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultPrompt"></param>
    /// <returns></returns>
    Task<string> GetPromptAndSetDefaultAsync(string key, string defaultPrompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Allow setting prompt value.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="prompt"></param>
    Task SetPromptAsync(string key, string prompt, CancellationToken cancellationToken = default);
}

public class NullPromptStore : IPromptStore
{
    public static NullPromptStore Instance { get; } = new NullPromptStore();

    /// <summary>
    /// Get the prompt for the given key.
    /// </summary>
    /// <param name="key">The key for which the prompt is requested.</param>
    /// <returns>An empty prompt.</returns>
    public Task<string?> GetPromptAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Return the default prompt always
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultPrompt"></param>
    /// <returns></returns>
    public Task<string> GetPromptAndSetDefaultAsync(string key, string defaultPrompt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string>(defaultPrompt);
    }

    /// <summary>
    /// Allow setting prompt value.
    /// </summary>
    /// <param name="key">The key for which the prompt is set.</param>
    /// <param name="prompt">The prompt value to set.</param>
    public Task SetPromptAsync(string key, string prompt, CancellationToken cancellationToken = default)
    {
        // No operation as this is a null implementation.
        return Task.CompletedTask;
    }
}