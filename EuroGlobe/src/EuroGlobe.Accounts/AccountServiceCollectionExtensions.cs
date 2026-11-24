using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Accounts.Services;
using EuroGlobe.Accounts.Transfers;
using EuroGlobe.Contracts.Interfaces;
using EuroGlobe.Contracts.Time;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Accounts;

public static class AccountServiceCollectionExtensions
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services)
    {
        services.AddScoped<IAccountRepository, InMemoryAccountRepository>();
        services.AddScoped<ITransactionRepository, InMemoryTransactionRepository>();
        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IAuditLog, ConsoleAuditLog>();
        services.AddScoped<IScaService, MockScaService>();
        services.AddScoped<ISepaGateway, MockSepaGateway>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<AccountManagementService>();

        return services;
    }
}