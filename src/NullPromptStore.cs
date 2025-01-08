using System.Threading.Tasks;

namespace KernelMemory.Extensions
{
    /// <summary>
    /// A null implementation of the IPromptStore interface that returns empty prompts.
    /// </summary>
    public class NullPromptStore : IPromptStore
    {
        /// <summary>
        /// Get the prompt for the given key.
        /// </summary>
        /// <param name="key">The key for which the prompt is requested.</param>
        /// <returns>An empty prompt.</returns>
        public Task<string> GetPromptAsync(string key)
        {
            return Task.FromResult(string.Empty);
        }

        /// <summary>
        /// Allow setting prompt value.
        /// </summary>
        /// <param name="key">The key for which the prompt is set.</param>
        /// <param name="prompt">The prompt value to set.</param>
        public Task SetPromptAsync(string key, string prompt)
        {
            // No operation as this is a null implementation.
            return Task.CompletedTask;
        }
    }
}
