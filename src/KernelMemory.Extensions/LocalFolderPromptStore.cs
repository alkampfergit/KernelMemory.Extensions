using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KernelMemory.Extensions;

public class LocalFolderPromptStore : BasePromptStore
{
    private readonly string _promptDirectory;

    public LocalFolderPromptStore(string promptDirectory, ILogger<LocalFolderPromptStore>? log = null)
        : base(log ?? DefaultLogger<LocalFolderPromptStore>.Instance)
    {
        _promptDirectory = promptDirectory;
        Directory.CreateDirectory(promptDirectory);
    }

    private string GetPromptFilePath(string key)
    {
        var sanitizedKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_promptDirectory, $"{sanitizedKey}.prompt");
    }

    public override async Task<string?> GetPromptAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetPromptFilePath(key);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public override async Task SetPromptAsync(string key, string prompt, CancellationToken cancellationToken = default)
    {
        var filePath = GetPromptFilePath(key);
        await File.WriteAllTextAsync(filePath, prompt, cancellationToken);
    }
}
