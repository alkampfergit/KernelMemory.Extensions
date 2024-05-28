namespace KernelMemory.Extensions.Cohere;

public class CohereConfiguration
{
    public string? ApiKey { get; set; }

    public string? HttpFactoryClientName { get; set; }

    /// <summary>
    /// Useful when it is hosted on azure ai studio or other services.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.cohere.ai/";
}