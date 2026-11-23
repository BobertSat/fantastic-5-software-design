using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Runs;
using Microsoft.EntityFrameworkCore;

namespace EuroGlobe.Jobs.Persistence;

/// <summary>The jobs module's own DbContext, in its own <c>jobs</c> schema (ADR-M001).</summary>
internal sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
    public const string Schema = "jobs";

    public DbSet<JobRunRow> JobRuns => Set<JobRunRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<JobRunRow>(run =>
        {
            run.ToTable("job_runs");
            run.HasKey(r => r.Id);
            run.Property(r => r.Id).ValueGeneratedNever();
            run.Property(r => r.JobName).HasMaxLength(100);
            run.Property(r => r.Trigger).HasConversion<string>().HasMaxLength(16);
            run.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            run.HasIndex(r => new { r.JobName, r.StartedAt });
            run.HasIndex(r => new { r.JobName, r.Trigger, r.ScheduledFor });
        });
    }
}

/// <summary>A row of <c>jobs.job_runs</c>.</summary>
internal sealed class JobRunRow
{
    public Guid Id { get; set; }

    public string JobName { get; set; } = "";

    public JobTrigger Trigger { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public JobRunStatus Status { get; set; }

    public string? Error { get; set; }

    public int ItemsProcessed { get; set; }

    public int ItemsFailed { get; set; }
}
