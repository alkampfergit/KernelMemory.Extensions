using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KernelMemory.Extensions.FunctionalTests;

public class Startup
{
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.development.json", optional: true)
            .AddUserSecrets<Startup>()
            .AddEnvironmentVariables()
            .Build();

        hostBuilder.ConfigureServices(services =>
        {
            services.AddLogging(logging =>
            {
                logging.AddConsole();
            });
        });

        hostBuilder.ConfigureHostConfiguration(builder => builder.AddConfiguration(Configuration));
    }

    internal static IConfiguration Configuration { get; private set; } = null!;
}
