using Microsoft.Extensions.Options;

namespace EuroGlobe.Mortgages;

/// <summary>The <c>Mortgages</c> configuration section. Rates and ratios are fractions (0.0365 = 3.65 %).</summary>
public sealed class MortgageOptions
{
    public const string SectionName = "Mortgages";

    public decimal FixedAnnualRate { get; set; } = 0.0365m;

    public decimal VariableMargin { get; set; } = 0.0140m;

    /// <summary>Used when no Euribor has been set yet.</summary>
    public decimal DefaultEuribor { get; set; } = 0.0275m;

    public decimal MaxDti { get; set; } = 0.35m;

    public decimal MaxLtv { get; set; } = 0.85m;

    /// <summary>Rate shock for the informational stress test, in basis points.</summary>
    public int StressTestBps { get; set; } = 200;

    public int MinTermMonths { get; set; } = 60;

    public int MaxTermMonths { get; set; } = 360;

    public decimal MinAmount { get; set; } = 10_000m;

    public decimal MaxAmount { get; set; } = 2_000_000m;

    /// <summary>Days after the due date before an overdue mortgage becomes Delinquent (0 = on the due date).</summary>
    public int DelinquencyGraceDays { get; set; }

    public int TerminationAfterDelinquentDays { get; set; } = 90;

    public TimeOnly RepaymentJobTimeUtc { get; set; } = new(2, 0);

    public CreditBureauOptions CreditBureau { get; set; } = new();
}

public sealed class CreditBureauOptions
{
    public int MinScore { get; set; } = 600;

    /// <summary>Users the mock bureau always scores 450, to demo a rejection.</summary>
    public List<Guid> BlockedUserIds { get; set; } = [];
}

/// <summary>Rejects impossible configuration at startup instead of producing wrong numbers later.</summary>
internal sealed class MortgageOptionsValidator : IValidateOptions<MortgageOptions>
{
    internal const int MaxTermLimit = 600;
    internal const int MinCreditScore = 300;
    internal const int MaxCreditScore = 850;

    public ValidateOptionsResult Validate(string? name, MortgageOptions options)
    {
        var errors = new List<string>();

        void Fraction(decimal value, string key, bool allowZero = true)
        {
            if (value < 0 || value >= 1 || (!allowZero && value == 0))
            {
                errors.Add($"{key} must be a fraction {(allowZero ? "from 0" : "above 0")} and below 1 (e.g. 0.0365 for 3.65 %).");
            }
        }

        Fraction(options.FixedAnnualRate, "FixedAnnualRate");
        Fraction(options.VariableMargin, "VariableMargin");
        Fraction(options.DefaultEuribor, "DefaultEuribor");

        if (options.MaxDti is <= 0 or > 1)
        {
            errors.Add("MaxDti must be above 0 and at most 1.");
        }

        if (options.MaxLtv is <= 0 or > 1)
        {
            errors.Add("MaxLtv must be above 0 and at most 1.");
        }

        if (options.StressTestBps < 0)
        {
            errors.Add("StressTestBps must not be negative.");
        }

        if (options.MinTermMonths < 1 || options.MaxTermMonths > MaxTermLimit || options.MinTermMonths > options.MaxTermMonths)
        {
            errors.Add($"Term limits must satisfy 1 <= MinTermMonths <= MaxTermMonths <= {MaxTermLimit}.");
        }

        if (options.MinAmount <= 0 || options.MinAmount > options.MaxAmount)
        {
            errors.Add("Amount limits must satisfy 0 < MinAmount <= MaxAmount.");
        }

        if (options.DelinquencyGraceDays < 0)
        {
            errors.Add("DelinquencyGraceDays must not be negative.");
        }

        if (options.TerminationAfterDelinquentDays < 1)
        {
            errors.Add("TerminationAfterDelinquentDays must be at least 1.");
        }

        if (options.CreditBureau.MinScore is < MinCreditScore or > MaxCreditScore)
        {
            errors.Add($"CreditBureau:MinScore must be between {MinCreditScore} and {MaxCreditScore}.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors.Select(e => $"{MortgageOptions.SectionName}:{e}"));
    }
}
