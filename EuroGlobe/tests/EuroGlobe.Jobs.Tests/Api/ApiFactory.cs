using System.Net.Http.Json;
using System.Text.Json;
using EuroGlobe.Jobs.Tests.TestDoubles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EuroGlobe.Jobs.Tests.Api;

/// <summary>
/// The real API (<c>Program</c>) in memory, with <see cref="MinuteJob"/> registered. Every jobs/demo setting
/// is set explicitly so a developer's local appsettings.Development.json can't change the result.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Dictionary<string, string?> _settings = new()
    {
        ["ConnectionStrings:EuroGlobe"] = "",
        ["Demo:EnableAdminEndpoints"] = "true",
        ["Jobs:SchedulerEnabled"] = "false",
        ["Jobs:UseSimulatedClock"] = "true",
    };

    public ApiFactory(string environment = "Development", params (string Key, string Value)[] overrides)
    {
        _environment = environment;
        foreach (var (key, value) in overrides)
        {
            _settings[key] = value;
        }
    }

    public JobProbe Probe => Services.GetRequiredService<JobProbe>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<JobProbe>();
            services.AddScoped<ScopedDependency>();
            services.AddRecurringJob<MinuteJob>();
        });
    }

    public static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
