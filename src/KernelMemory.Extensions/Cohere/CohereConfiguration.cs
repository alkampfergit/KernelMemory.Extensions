using Microsoft.Extensions.DependencyInjection;

namespace KernelMemory.Extensions.Cohere;

public abstract class CohereConfiguration
{
    public string? ApiKey { get; set; }

    public string? ClientKeyedName { get; set; }

    /// <summary>
    /// Useful when it is hosted on azure ai studio or other services.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.cohere.ai/";
}

public class CohereReRankConfiguration : CohereConfiguration;

public class CohereChatConfiguration : CohereConfiguration;

public class CohereEmbedConfiguration : CohereConfiguration;

public static class CohereConfigurationHelper
{
    public static IServiceCollection ConfigureCohereKeyed(
        this IServiceCollection services,
        string keyedName,
        string apiKey,
        string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddKeyedSingleton(keyedName, new CohereReRankConfiguration
        {
            ApiKey = apiKey,
            ClientKeyedName = keyedName,
            BaseUrl = baseUrl,
        });

        services.AddKeyedSingleton(keyedName, new CohereChatConfiguration
        {
            ApiKey = apiKey,
            ClientKeyedName = keyedName,
            BaseUrl = baseUrl,
        });

        services.AddKeyedSingleton(keyedName, new CohereEmbedConfiguration
        {
            ApiKey = apiKey,
            ClientKeyedName = keyedName,
            BaseUrl = baseUrl,
        });

        return services;
    }

    public static IServiceCollection ConfigureCohere(
        this IServiceCollection services,
        string apiKey,
        string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddSingleton(new CohereReRankConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        services.AddSingleton(new CohereChatConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        services.AddSingleton(new CohereEmbedConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        return services;
    }

    public static IServiceCollection ConfigureCohereRerank(
            this IServiceCollection services,
            string apiKey,
            string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddSingleton(new CohereReRankConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        return services;
    }

    public static IServiceCollection ConfigureCohereChat(
            this IServiceCollection services,
            string apiKey,
            string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddSingleton(new CohereChatConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        return services;
    }

    public static IServiceCollection ConfigureCohereEmbed(
            this IServiceCollection services,
            string apiKey,
            string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddSingleton(new CohereEmbedConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        return services;
    }

    public static IServiceCollection ConfigureCohereRerankKeyed(
        this IServiceCollection services,
        string keyedName,
        string apiKey,
        string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddKeyedSingleton(keyedName, new CohereReRankConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        return services;
    }

    public static IServiceCollection ConfigureCohereChatKeyed(
        this IServiceCollection services,
        string keyedName,
        string apiKey,
        string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddKeyedSingleton(keyedName, new CohereChatConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        return services;
    }

    public static IServiceCollection ConfigureCohereEmbedKeyed(
        this IServiceCollection services,
        string keyedName,
        string apiKey,
        string baseUrl = "https://api.cohere.ai/")
    {
        //configure all three type of configuratio with the same config

        services.AddKeyedSingleton(keyedName, new CohereEmbedConfiguration
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
        });

        return services;
    }
}