using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace EuroGlobe.Mortgages.Tests;

internal static class TestSupport
{
    public static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    public static IHostEnvironment Environment(string name) => new HostingEnvironment { EnvironmentName = name };

    /// <summary>The production registration (<c>AddMortgagesModule</c>) with the given settings.</summary>
    public static ServiceProvider BuildModule(params (string Key, string? Value)[] settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Environment(Environments.Development));
        services.AddMortgagesModule(Configuration(settings));
        return services.BuildServiceProvider(validateScopes: true);
    }
}
