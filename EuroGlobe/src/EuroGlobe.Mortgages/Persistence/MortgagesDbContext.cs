using EuroGlobe.Mortgages.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EuroGlobe.Mortgages.Persistence;

/// <summary>The mortgage module's own DbContext in the <c>mortgages</c> schema (ADR-M001).</summary>
internal sealed class MortgagesDbContext(DbContextOptions<MortgagesDbContext> options) : DbContext(options)
{
    public const string Schema = "mortgages";

    private const string Money = "numeric(19,2)";
    private const string Rate = "numeric(9,6)";
    private const int EnumLength = 20;

    public DbSet<Mortgage> Mortgages => Set<Mortgage>();

    public DbSet<Installment> Installments => Set<Installment>();

    public DbSet<StateTransition> StateTransitions => Set<StateTransition>();

    public DbSet<RateChange> RateChanges => Set<RateChange>();

    public DbSet<EuriborRate> EuriborRates => Set<EuriborRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Mortgage>(ConfigureMortgage);
        modelBuilder.Entity<Installment>(ConfigureInstallment);
        modelBuilder.Entity<StateTransition>(ConfigureStateTransition);
        modelBuilder.Entity<RateChange>(ConfigureRateChange);
        modelBuilder.Entity<EuriborRate>(ConfigureEuriborRate);
    }

    private static void ConfigureMortgage(EntityTypeBuilder<Mortgage> m)
    {
        m.ToTable("mortgages");
        m.HasKey(x => x.Id);
        m.Property(x => x.Id).ValueGeneratedNever();
        m.HasIndex(x => x.UserId);

        foreach (var money in new[] { nameof(Mortgage.PropertyValue), nameof(Mortgage.DownPayment), nameof(Mortgage.PrincipalAmount),
                     nameof(Mortgage.NetMonthlyIncome), nameof(Mortgage.ExistingMonthlyDebt), nameof(Mortgage.MonthlyPayment),
                     nameof(Mortgage.RemainingPrincipal) })
        {
            m.Property(money).HasColumnType(Money);
        }

        foreach (var rate in new[] { nameof(Mortgage.LtvRatio), nameof(Mortgage.DtiRatio), nameof(Mortgage.StressDtiRatio),
                     nameof(Mortgage.AnnualRate), nameof(Mortgage.Margin) })
        {
            m.Property(rate).HasColumnType(Rate);
        }

        m.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(EnumLength);
        m.Property(x => x.State).HasConversion<string>().HasMaxLength(EnumLength);
        m.Property(x => x.RateType).HasConversion<string>().HasMaxLength(EnumLength);
        m.Property(x => x.ClosureReason).HasConversion<string>().HasMaxLength(EnumLength);
        m.Property(x => x.IncomeProofDocumentRef).HasMaxLength(500);
        m.Property(x => x.RejectionReasons).HasColumnType("text[]");
        m.Property(x => x.Version).IsRowVersion(); // Npgsql maps this to the xmin system column

        m.OwnsOne(x => x.ScoringReport, report =>
        {
            report.ToJson();
            report.OwnsMany(r => r.Rules);
        });

        m.HasMany(x => x.Installments).WithOne().HasForeignKey(i => i.MortgageId).OnDelete(DeleteBehavior.Cascade);
        m.HasMany(x => x.StateTransitions).WithOne().HasForeignKey(t => t.MortgageId).OnDelete(DeleteBehavior.Cascade);
        m.HasMany(x => x.RateChanges).WithOne().HasForeignKey(c => c.MortgageId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInstallment(EntityTypeBuilder<Installment> i)
    {
        i.ToTable("mortgage_installments");
        i.HasKey(x => x.Id);
        i.Property(x => x.Id).ValueGeneratedNever();
        i.HasIndex(x => new { x.MortgageId, x.Seq }).IsUnique();
        i.HasIndex(x => new { x.Status, x.DueDate });
        i.Property(x => x.PrincipalPart).HasColumnType(Money);
        i.Property(x => x.InterestPart).HasColumnType(Money);
        i.Property(x => x.Amount).HasColumnType(Money);
        i.Property(x => x.PaidAmount).HasColumnType(Money);
        i.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumLength);
    }

    private static void ConfigureStateTransition(EntityTypeBuilder<StateTransition> t)
    {
        t.ToTable("mortgage_state_transitions");
        t.HasKey(x => x.Id);
        t.Property(x => x.Id).ValueGeneratedNever();
        t.HasIndex(x => new { x.MortgageId, x.At });
        t.Property(x => x.FromState).HasConversion<string>().HasMaxLength(EnumLength);
        t.Property(x => x.ToState).HasConversion<string>().HasMaxLength(EnumLength);
        t.Property(x => x.Reason).HasMaxLength(500);
        t.Property(x => x.Actor).HasMaxLength(64);
    }

    private static void ConfigureRateChange(EntityTypeBuilder<RateChange> c)
    {
        c.ToTable("mortgage_rate_changes");
        c.HasKey(x => x.Id);
        c.Property(x => x.Id).ValueGeneratedNever();
        c.HasIndex(x => new { x.MortgageId, x.At });
        c.Property(x => x.OldEuribor).HasColumnType(Rate);
        c.Property(x => x.NewEuribor).HasColumnType(Rate);
        c.Property(x => x.OldRate).HasColumnType(Rate);
        c.Property(x => x.NewRate).HasColumnType(Rate);
        c.Property(x => x.OldPayment).HasColumnType(Money);
        c.Property(x => x.NewPayment).HasColumnType(Money);
    }

    private static void ConfigureEuriborRate(EntityTypeBuilder<EuriborRate> e)
    {
        e.ToTable("euribor_rates");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => x.EffectiveAt);
        e.Property(x => x.Rate).HasColumnType(Rate);
        e.Property(x => x.SetBy).HasMaxLength(64);
    }
}
