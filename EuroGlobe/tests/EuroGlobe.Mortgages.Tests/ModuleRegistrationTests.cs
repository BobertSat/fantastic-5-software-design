using System.Text.Json;
using EuroGlobe.Mortgages.Application;
using EuroGlobe.Mortgages.Domain;
using EuroGlobe.Mortgages.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Mortgages.Tests;

public sealed class ModuleRegistrationTests
{
    [Fact]
    public void AddMortgagesModule_RegistersPlaceholders()
    {
        using var provider = TestSupport.BuildModule();
        using var scope = provider.CreateScope();

        Assert.IsType<HttpCurrentUser>(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
        Assert.IsType<AlwaysVerifiedKycGate>(provider.GetRequiredService<IMortgageKycGate>());
    }

    [Fact]
    public void AddMortgagesModule_RealServicesRegisteredFirst_Win()
    {
        var kyc = new DenyAllKycGate();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(TestSupport.Environment("Development"));
        services.AddSingleton<IMortgageKycGate>(kyc);

        services.AddMortgagesModule(TestSupport.Configuration());

        using var provider = services.BuildServiceProvider();
        Assert.Same(kyc, provider.GetRequiredService<IMortgageKycGate>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void DbContext_WithoutConnectionString_FailsOnlyWhenUsed(string? connectionString)
    {
        using var provider = TestSupport.BuildModule(("ConnectionStrings:EuroGlobe", connectionString));
        using var scope = provider.CreateScope();

        var ex = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<MortgagesDbContext>());

        Assert.Equal(MortgagesModule.MissingConnectionStringMessage, ex.Message);
    }

    [Fact]
    public void DbContext_WithConnectionString_UsesMortgagesSchema()
    {
        using var provider = TestSupport.BuildModule(("ConnectionStrings:EuroGlobe", "Host=localhost;Database=unused;Username=u;Password=p"));
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<MortgagesDbContext>();

        Assert.Equal("mortgages", db.Model.GetDefaultSchema());
    }

    [Theory]
    [InlineData(MortgageState.UnderReview, "\"UnderReview\"")]
    [InlineData(MortgagePurpose.BuyToLet, "\"BuyToLet\"")]
    [InlineData(RateType.Variable, "\"Variable\"")]
    [InlineData(ClosureReason.Terminated, "\"Terminated\"")]
    [InlineData(InstallmentStatus.Overdue, "\"Overdue\"")]
    public void Enums_SerializeAsNames(object value, string json)
    {
        Assert.Equal(json, JsonSerializer.Serialize(value, value.GetType()));
    }

    private sealed class DenyAllKycGate : IMortgageKycGate
    {
        public Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
