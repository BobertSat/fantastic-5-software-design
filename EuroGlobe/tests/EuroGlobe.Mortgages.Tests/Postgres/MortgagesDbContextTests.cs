using EuroGlobe.Mortgages.Domain;
using EuroGlobe.Mortgages.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EuroGlobe.Mortgages.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public sealed class MortgagesDbContextTests(PostgresFixture postgres) : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 10, 0, 0, TimeSpan.Zero);
    private readonly List<ServiceProvider> _providers = [];

    private async Task<Func<MortgagesDbContext>> NewDatabaseAsync()
    {
        var provider = TestSupport.BuildModule(("ConnectionStrings:EuroGlobe", await postgres.CreateDatabaseAsync()));
        _providers.Add(provider);
        return () => provider.CreateScope().ServiceProvider.GetRequiredService<MortgagesDbContext>();
    }

    private static Mortgage FullMortgage() => new()
    {
        UserId = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        PropertyValue = 250_000m,
        DownPayment = 50_000m,
        PrincipalAmount = 200_000m,
        TermMonths = 300,
        Purpose = MortgagePurpose.BuyToLet,
        NetMonthlyIncome = 4_800m,
        ExistingMonthlyDebt = 250m,
        IncomeProofDocumentRef = "docs/bank_statement.pdf",
        CreditCheckConsent = true,
        CreditScore = 785,
        LtvRatio = 0.8m,
        DtiRatio = 0.275521m,
        StressDtiRatio = 0.312345m,
        ScoringReport = new ScoringReport
        {
            Rules =
            [
                new ScoringRuleResult { Name = "LTV", Value = 0.8m, Limit = 0.85m, Passed = true },
                new ScoringRuleResult { Name = "StressTest", Value = 0.312345m, Limit = 0.35m, Passed = true, Informational = true },
            ],
        },
        RejectionReasons = ["first reason", "second reason"],
        State = MortgageState.Delinquent,
        RateType = RateType.Variable,
        AnnualRate = 0.0415m,
        Margin = 0.014m,
        MonthlyPayment = 1_072.32m,
        RemainingPrincipal = 198_420.55m,
        RemainingTermMonths = 296,
        NextDueDate = new DateOnly(2026, 12, 28),
        IsSubscribedToEuribor = true,
        SignedAt = Now.AddMonths(-4),
        DisbursedAt = Now.AddMonths(-4),
        DelinquentSince = new DateOnly(2026, 11, 28),
        CreatedAt = Now.AddMonths(-5),
        UpdatedAt = Now,
    };

    [Fact]
    public async Task Migration_FreshDatabase_CreatesMortgagesSchemaTablesAndOwnHistory()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'mortgages' ORDER BY table_name", connection);
        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(
            ["__EFMigrationsHistory", "euribor_rates", "mortgage_installments", "mortgage_rate_changes", "mortgage_state_transitions", "mortgages"],
            tables);
    }

    [Theory]
    [InlineData("mortgages", "principal_amount", "numeric", 19, 2)]
    [InlineData("mortgages", "annual_rate", "numeric", 9, 6)]
    [InlineData("mortgages", "dti_ratio", "numeric", 9, 6)]
    [InlineData("mortgage_installments", "amount", "numeric", 19, 2)]
    [InlineData("euribor_rates", "rate", "numeric", 9, 6)]
    public async Task Migration_MoneyAndRateColumns_HaveAgreedPrecision(string table, string column, string type, int precision, int scale)
    {
        await using var connection = new NpgsqlConnection(await postgres.CreateDatabaseAsync());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT data_type, numeric_precision, numeric_scale FROM information_schema.columns " +
            "WHERE table_schema = 'mortgages' AND table_name = @t AND column_name = @c", connection);
        command.Parameters.AddWithValue("t", table);
        command.Parameters.AddWithValue("c", column);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal((type, precision, scale), (reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
    }

    [Fact]
    public async Task SaveAndLoad_FullMortgage_RoundTripsEveryColumn()
    {
        var newContext = await NewDatabaseAsync();
        var mortgage = FullMortgage();
        mortgage.Installments.Add(new Installment
        {
            Seq = 1, DueDate = new DateOnly(2026, 8, 28), PrincipalPart = 380.15m, InterestPart = 692.17m, Amount = 1_072.32m,
            Status = InstallmentStatus.Paid, PaidAt = Now.AddMonths(-3), PaidAmount = 1_072.32m,
        });
        mortgage.Installments.Add(new Installment
        {
            Seq = 2, DueDate = new DateOnly(2026, 9, 28), PrincipalPart = 381.46m, InterestPart = 690.86m, Amount = 1_072.32m,
            Status = InstallmentStatus.Overdue,
        });
        mortgage.StateTransitions.Add(new StateTransition { FromState = null, ToState = MortgageState.UnderReview, At = Now, Actor = "user:x" });
        mortgage.StateTransitions.Add(new StateTransition
        {
            FromState = MortgageState.Active, ToState = MortgageState.Delinquent, Reason = "Installment 2 overdue", At = Now, Actor = "system",
        });
        mortgage.RateChanges.Add(new RateChange
        {
            OldEuribor = 0.0275m, NewEuribor = 0.03m, OldRate = 0.0415m, NewRate = 0.044m, OldPayment = 1_072.32m, NewPayment = 1_100.01m, At = Now,
        });

        await using (var db = newContext())
        {
            db.Mortgages.Add(mortgage);
            db.EuriborRates.Add(new EuriborRate { Rate = 0.0275m, EffectiveAt = Now, SetBy = "seed" });
            await db.SaveChangesAsync();
        }

        await using var read = newContext();
        var loaded = await read.Mortgages
            .Include(m => m.Installments)
            .Include(m => m.StateTransitions)
            .Include(m => m.RateChanges)
            .SingleAsync(m => m.Id == mortgage.Id);

        Assert.Equal(
            (mortgage.UserId, mortgage.AccountId, 250_000m, 50_000m, 200_000m, 300, MortgagePurpose.BuyToLet),
            (loaded.UserId, loaded.AccountId, loaded.PropertyValue, loaded.DownPayment, loaded.PrincipalAmount, loaded.TermMonths, loaded.Purpose));
        Assert.Equal((4_800m, 250m, "docs/bank_statement.pdf", true), (loaded.NetMonthlyIncome, loaded.ExistingMonthlyDebt, loaded.IncomeProofDocumentRef, loaded.CreditCheckConsent));
        Assert.Equal((785, 0.8m, 0.275521m, 0.312345m), (loaded.CreditScore, loaded.LtvRatio, loaded.DtiRatio, loaded.StressDtiRatio));
        Assert.Equal(["first reason", "second reason"], loaded.RejectionReasons);
        Assert.Equal((MortgageState.Delinquent, RateType.Variable, 0.0415m, 0.014m, 1_072.32m), (loaded.State, loaded.RateType, loaded.AnnualRate, loaded.Margin, loaded.MonthlyPayment));
        Assert.Equal((198_420.55m, 296, new DateOnly(2026, 12, 28), true), (loaded.RemainingPrincipal, loaded.RemainingTermMonths, loaded.NextDueDate, loaded.IsSubscribedToEuribor));
        Assert.Equal((mortgage.SignedAt, mortgage.DisbursedAt, new DateOnly(2026, 11, 28)), (loaded.SignedAt, loaded.DisbursedAt, loaded.DelinquentSince));
        Assert.Null(loaded.ClosedAt);
        Assert.Null(loaded.ClosureReason);
        Assert.Equal((mortgage.CreatedAt, mortgage.UpdatedAt), (loaded.CreatedAt, loaded.UpdatedAt));
        Assert.NotEqual(0u, loaded.Version);

        var report = Assert.IsType<ScoringReport>(loaded.ScoringReport);
        Assert.Equal(["LTV", "StressTest"], report.Rules.Select(r => r.Name));
        Assert.True(report.Rules[1].Informational);
        Assert.Equal(0.312345m, report.Rules[1].Value);

        Assert.Equal([1, 2], loaded.Installments.OrderBy(i => i.Seq).Select(i => i.Seq));
        var paid = loaded.Installments.Single(i => i.Seq == 1);
        Assert.Equal((InstallmentStatus.Paid, 1_072.32m, new DateOnly(2026, 8, 28)), (paid.Status, paid.PaidAmount, paid.DueDate));
        Assert.Null(loaded.Installments.Single(i => i.Seq == 2).PaidAt);

        Assert.Contains(loaded.StateTransitions, t => t.FromState is null && t.ToState == MortgageState.UnderReview);
        Assert.Contains(loaded.StateTransitions, t => t.ToState == MortgageState.Delinquent && t.Reason == "Installment 2 overdue" && t.Actor == "system");
        var change = Assert.Single(loaded.RateChanges);
        Assert.Equal((0.03m, 0.044m, 1_100.01m), (change.NewEuribor, change.NewRate, change.NewPayment));
        Assert.Equal(0.0275m, (await read.EuriborRates.SingleAsync()).Rate);
    }

    [Fact]
    public async Task Save_MoneyWithMoreThanTwoDecimals_IsRoundedByTheColumn()
    {
        var newContext = await NewDatabaseAsync();
        var mortgage = FullMortgage();
        mortgage.RemainingPrincipal = 100.005m;
        mortgage.AnnualRate = 0.04150049m;
        await using (var db = newContext())
        {
            db.Mortgages.Add(mortgage);
            await db.SaveChangesAsync();
        }

        await using var read = newContext();
        var loaded = await read.Mortgages.SingleAsync();

        Assert.Equal(100.01m, loaded.RemainingPrincipal);
        Assert.Equal(0.041500m, loaded.AnnualRate);
    }

    [Fact]
    public async Task Save_ConcurrentUpdateOfSameMortgage_SecondWriterGetsConcurrencyError()
    {
        var newContext = await NewDatabaseAsync();
        var mortgage = FullMortgage();
        await using (var db = newContext())
        {
            db.Mortgages.Add(mortgage);
            await db.SaveChangesAsync();
        }

        await using var first = newContext();
        await using var second = newContext();
        var a = await first.Mortgages.SingleAsync();
        var b = await second.Mortgages.SingleAsync();
        a.State = MortgageState.Active;
        await first.SaveChangesAsync();
        b.State = MortgageState.Closed;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Delete_Mortgage_CascadesToItsChildRows()
    {
        var newContext = await NewDatabaseAsync();
        var mortgage = FullMortgage();
        mortgage.Installments.Add(new Installment { Seq = 1, DueDate = new DateOnly(2026, 12, 1), Amount = 1m });
        await using (var db = newContext())
        {
            db.Mortgages.Add(mortgage);
            await db.SaveChangesAsync();
            db.Mortgages.Remove(mortgage);
            await db.SaveChangesAsync();
        }

        await using var read = newContext();
        Assert.Equal(0, await read.Installments.CountAsync());
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
    }
}
