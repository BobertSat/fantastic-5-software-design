using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EuroGlobe.Mortgages.Tests;

public sealed class MortgageOptionsTests
{
    private static ValidateOptionsResult Validate(Action<MortgageOptions> change)
    {
        var options = new MortgageOptions();
        change(options);
        return new MortgageOptionsValidator().Validate(null, options);
    }

    [Fact]
    public void Defaults_MatchTheAgreedRulesAndAreValid()
    {
        var options = new MortgageOptions();

        Assert.Equal(0.0365m, options.FixedAnnualRate);
        Assert.Equal(0.0140m, options.VariableMargin);
        Assert.Equal(0.0275m, options.DefaultEuribor);
        Assert.Equal(0.35m, options.MaxDti);
        Assert.Equal(0.85m, options.MaxLtv);
        Assert.Equal(200, options.StressTestBps);
        Assert.Equal((60, 360), (options.MinTermMonths, options.MaxTermMonths));
        Assert.Equal((10_000m, 2_000_000m), (options.MinAmount, options.MaxAmount));
        Assert.Equal(0, options.DelinquencyGraceDays);
        Assert.Equal(90, options.TerminationAfterDelinquentDays);
        Assert.Equal(new TimeOnly(2, 0), options.RepaymentJobTimeUtc);
        Assert.Equal(600, options.CreditBureau.MinScore);
        Assert.Empty(options.CreditBureau.BlockedUserIds);
        Assert.True(new MortgageOptionsValidator().Validate(null, options).Succeeded);
    }

    public static TheoryData<string, Action<MortgageOptions>> InvalidSettings => new()
    {
        { "FixedAnnualRate", o => o.FixedAnnualRate = -0.01m },
        { "FixedAnnualRate", o => o.FixedAnnualRate = 1m },
        { "VariableMargin", o => o.VariableMargin = 1.4m },
        { "DefaultEuribor", o => o.DefaultEuribor = -0.001m },
        { "MaxDti", o => o.MaxDti = 0m },
        { "MaxDti", o => o.MaxDti = 1.01m },
        { "MaxLtv", o => o.MaxLtv = 0m },
        { "MaxLtv", o => o.MaxLtv = 1.5m },
        { "StressTestBps", o => o.StressTestBps = -1 },
        { "Term limits", o => o.MinTermMonths = 0 },
        { "Term limits", o => o.MaxTermMonths = 601 },
        { "Term limits", o => (o.MinTermMonths, o.MaxTermMonths) = (120, 60) },
        { "Amount limits", o => o.MinAmount = 0m },
        { "Amount limits", o => (o.MinAmount, o.MaxAmount) = (50_000m, 10_000m) },
        { "DelinquencyGraceDays", o => o.DelinquencyGraceDays = -1 },
        { "TerminationAfterDelinquentDays", o => o.TerminationAfterDelinquentDays = 0 },
        { "CreditBureau:MinScore", o => o.CreditBureau.MinScore = 299 },
        { "CreditBureau:MinScore", o => o.CreditBureau.MinScore = 851 },
    };

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Validate_InvalidSetting_FailsNamingIt(string setting, Action<MortgageOptions> change)
    {
        var result = Validate(change);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.StartsWith($"Mortgages:{setting}"));
    }

    public static TheoryData<Action<MortgageOptions>> BoundaryValues => new()
    {
        o => o.FixedAnnualRate = 0m,
        o => o.MaxDti = 1m,
        o => o.MaxLtv = 1m,
        o => o.StressTestBps = 0,
        o => (o.MinTermMonths, o.MaxTermMonths) = (1, 600),
        o => (o.MinAmount, o.MaxAmount) = (1m, 1m),
        o => o.TerminationAfterDelinquentDays = 1,
        o => o.CreditBureau.MinScore = 300,
        o => o.CreditBureau.MinScore = 850,
    };

    [Theory]
    [MemberData(nameof(BoundaryValues))]
    public void Validate_BoundaryValue_Succeeds(Action<MortgageOptions> change)
    {
        Assert.True(Validate(change).Succeeded);
    }

    [Fact]
    public void Validate_SeveralProblems_ReportsAll()
    {
        var result = Validate(o => (o.MaxDti, o.StressTestBps) = (0m, -5));

        Assert.Equal(2, result.Failures!.Count());
    }

    [Fact]
    public void Binding_FromConfiguration_ReadsEverySetting()
    {
        var blocked = Guid.NewGuid();
        using var provider = TestSupport.BuildModule(
            ("Mortgages:FixedAnnualRate", "0.04"),
            ("Mortgages:RepaymentJobTimeUtc", "03:30"),
            ("Mortgages:CreditBureau:MinScore", "650"),
            ("Mortgages:CreditBureau:BlockedUserIds:0", blocked.ToString()));

        var options = provider.GetRequiredService<IOptions<MortgageOptions>>().Value;

        Assert.Equal(0.04m, options.FixedAnnualRate);
        Assert.Equal(new TimeOnly(3, 30), options.RepaymentJobTimeUtc);
        Assert.Equal(650, options.CreditBureau.MinScore);
        Assert.Equal([blocked], options.CreditBureau.BlockedUserIds);
    }

    [Fact]
    public async Task HostStart_InvalidConfiguration_FailsFast()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([new("Mortgages:MaxLtv", "1.2")]);
        builder.Services.AddMortgagesModule(builder.Configuration);
        using var host = builder.Build();

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(ex.Failures, f => f.Contains("MaxLtv"));
    }
}
