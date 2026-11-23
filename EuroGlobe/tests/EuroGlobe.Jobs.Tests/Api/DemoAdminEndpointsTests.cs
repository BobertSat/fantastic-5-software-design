using System.Net;
using EuroGlobe.Api.Controllers.Demo;
using EuroGlobe.Jobs.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EuroGlobe.Jobs.Tests.Api;

public sealed class DemoAdminEndpointsTests
{
    [Theory]
    [InlineData("/api/admin/jobs")]
    [InlineData("/api/admin/clock")]
    public async Task AdminEndpoints_Disabled_DoNotExist(string path)
    {
        await using var api = new ApiFactory(overrides: ("Demo:EnableAdminEndpoints", "false"));

        var response = await api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task OpenApiDocument_ListsAdminEndpointsOnlyWhenEnabled(string enabled, bool expected)
    {
        await using var api = new ApiFactory(overrides: ("Demo:EnableAdminEndpoints", enabled));

        var document = await api.CreateClient().GetStringAsync("/openapi/v1.json");

        Assert.Equal(expected, document.Contains("/api/admin/jobs"));
        Assert.Equal(expected, document.Contains("/api/admin/clock"));
    }

    [Fact]
    public void AddDemoAdminEndpoints_EnabledOutsideDevelopment_Throws()
    {
        var services = new ServiceCollection();
        var configuration = JobsTestContext.Configuration((DemoAdminEndpointsExtensions.EnableSetting, "true"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddDemoAdminEndpoints(configuration, JobsTestContext.Environment(Environments.Production)));

        Assert.Contains("Development", ex.Message);
    }

    [Fact]
    public void AddDemoAdminEndpoints_DisabledOutsideDevelopment_IsAllowed()
    {
        var services = new ServiceCollection();

        services.AddDemoAdminEndpoints(JobsTestContext.Configuration(), JobsTestContext.Environment(Environments.Production));

        Assert.NotEmpty(services);
    }
}
