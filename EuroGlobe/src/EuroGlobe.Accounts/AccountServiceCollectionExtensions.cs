using EuroGlobe.Accounts.Services;
using EuroGlobe.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Accounts;

public static class AccountServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAccountsModule()
        {
            services.AddSingleton<IAccountService, AccountService>();

            return services;
        }
    }
}