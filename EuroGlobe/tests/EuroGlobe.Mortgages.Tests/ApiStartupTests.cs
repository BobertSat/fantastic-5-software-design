using System.Net;
using EuroGlobe.Mortgages.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EuroGlobe.Mortgages.Tests;

/// <summary>The real API (<c>Program</c>) starts with the mortgage module registered.</summary>
public sealed class ApiStartupTests
{
    private sealed class Api(params (string Key, string Value)[] settings) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:EuroGlobe", "");
            builder.UseSetting("Jobs:SchedulerEnabled", "false");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        }
    }

    [Fact]
    public async Task Api_WithMortgagesModule_StartsAndServesRequests()
    {
        await using var api = new Api();

        var response = await api.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0.0365m, api.Services.GetRequiredService<IOptions<MortgageOptions>>().Value.FixedAnnualRate);
    }

    [Fact]
    public async Task Api_WithoutDatabase_StartsButMortgagesDbContextExplainsWhatIsMissing()
    {
        await using var api = new Api();
        using var scope = api.Services.CreateScope();

        var ex = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<MortgagesDbContext>());

        Assert.Contains("ConnectionStrings:EuroGlobe", ex.Message);
    }

    [Fact]
    public async Task Api_InvalidMortgageConfiguration_FailsToStart()
    {
        await using var api = new Api(("Mortgages:MaxDti", "0"));

        var ex = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("MaxDti", ex.ToString());
    }
}
