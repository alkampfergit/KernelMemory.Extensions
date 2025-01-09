using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Threading;

namespace KernelMemory.Extensions;

public abstract class BasePromptStore : IPromptStore
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{\$\w+\}\}", RegexOptions.Compiled);

    protected readonly ILogger _log;

    protected BasePromptStore(ILogger log)
    {
        _log = log;
    }

    protected void ValidatePlaceholders(string defaultPrompt, string loadedPrompt)
    {
        var placeholders = PlaceholderRegex.Matches(defaultPrompt)
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        foreach (var placeholder in placeholders)
        {
            if (!loadedPrompt.Contains(placeholder))
            {
                _log.LogError("The prompt does not contain {Placeholder} placeholder, the prompt will not work correctly", placeholder);
            }
        }
    }

    public abstract Task<string?> GetPromptAsync(string key, CancellationToken cancellationToken = default);
    public abstract Task SetPromptAsync(string key, string prompt, CancellationToken cancellationToken = default);

    public virtual async Task<string> GetPromptAndSetDefaultAsync(string key, string defaultPrompt, CancellationToken cancellationToken = default)
    {
        var prompt = await GetPromptAsync(key, cancellationToken);
        if (prompt == null)
        {
            await SetPromptAsync(key, defaultPrompt, cancellationToken);
            return defaultPrompt;
        }

        ValidatePlaceholders(defaultPrompt, prompt);
        return prompt;
    }
}
