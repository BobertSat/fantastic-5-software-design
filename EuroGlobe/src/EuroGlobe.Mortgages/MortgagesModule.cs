using EuroGlobe.Mortgages.Application;
using EuroGlobe.Mortgages.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EuroGlobe.Mortgages;

public static class MortgagesModule
{
    public const string ConnectionStringName = "EuroGlobe";

    internal const string MissingConnectionStringMessage =
        "The mortgages module needs ConnectionStrings:EuroGlobe (see ADR-J005 for the local Docker setup).";

    /// <summary>
    /// Registers the mortgage module: options (validated at startup), the DbContext and the placeholders for
    /// teammates' services that don't exist yet. Real implementations registered by other modules win.
    /// </summary>
    public static IServiceCollection AddMortgagesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MortgageOptions>()
            .Bind(configuration.GetSection(MortgageOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MortgageOptions>, MortgageOptionsValidator>());

        // The app must still start without a database (teammates without Docker); only using it fails.
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        services.AddDbContext<MortgagesDbContext>(db =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(MissingConnectionStringMessage);
            }

            db.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", MortgagesDbContext.Schema))
                .UseSnakeCaseNamingConvention();
        });

        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
        services.TryAddSingleton<IMortgageKycGate, AlwaysVerifiedKycGate>();

        return services;
    }
}
