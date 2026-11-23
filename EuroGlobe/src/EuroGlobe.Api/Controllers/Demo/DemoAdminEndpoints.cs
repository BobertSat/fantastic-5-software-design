using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EuroGlobe.Api.Controllers.Demo;

/// <summary>
/// Marks a controller as a demo/admin tool (ADR-J006). It exists only when
/// <c>Demo:EnableAdminEndpoints=true</c>; otherwise it is not mapped and not in the OpenAPI document.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DemoAdminEndpointAttribute : Attribute;

public static class DemoAdminEndpointsExtensions
{
    public const string EnableSetting = "Demo:EnableAdminEndpoints";

    /// <exception cref="InvalidOperationException">Enabled outside the Development environment.</exception>
    public static IServiceCollection AddDemoAdminEndpoints(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var enabled = configuration.GetValue<bool>(EnableSetting);
        if (enabled && !environment.IsDevelopment())
        {
            throw new InvalidOperationException($"{EnableSetting} is allowed only in the Development environment.");
        }

        services.AddMvcCore().ConfigureApplicationPartManager(parts =>
            parts.FeatureProviders.Add(new DemoAdminControllerFilter(enabled)));
        return services;
    }

    /// <summary>RFC 7807 problem response with a machine-readable <c>code</c>.</summary>
    internal static ObjectResult ProblemWithCode(this ControllerBase controller, int status, string code, string title, string? detail = null)
    {
        var factory = controller.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(controller.HttpContext, status, title, detail: detail);
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

/// <summary>Removes <see cref="DemoAdminEndpointAttribute"/> controllers when demo endpoints are disabled.</summary>
internal sealed class DemoAdminControllerFilter(bool enabled) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (enabled)
        {
            return;
        }

        foreach (var controller in feature.Controllers.Where(c => c.IsDefined(typeof(DemoAdminEndpointAttribute), inherit: true)).ToList())
        {
            feature.Controllers.Remove(controller);
        }
    }
}
