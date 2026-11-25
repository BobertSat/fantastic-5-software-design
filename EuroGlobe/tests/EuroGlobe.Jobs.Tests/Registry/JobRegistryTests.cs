using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Jobs.Tests.Registry;

public sealed class JobRegistryTests
{
    [Fact]
    public void Jobs_RegisteredJobs_ListsNameScheduleAndType()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<DailyJob>().AddRecurringJob<MinuteJob>());

        var jobs = ctx.Registry.Jobs;

        Assert.Equal([DailyJob.JobName, MinuteJob.JobName], jobs.Select(j => j.Name));
        Assert.Equal(typeof(DailyJob), jobs[0].JobType);
        Assert.Equal(JobScheduleKind.Daily, jobs[0].Schedule.Kind);
        Assert.Equal(TimeSpan.FromMinutes(1), jobs[1].Schedule.Interval);
    }

    [Fact]
    public void Jobs_NoJobsRegistered_IsEmpty()
    {
        using var ctx = JobsTestContext.Create(_ => { });

        Assert.Empty(ctx.Registry.Jobs);
    }

    [Fact]
    public void Find_KnownAndUnknownName_ReturnsJobOrNull()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<DailyJob>());

        Assert.Equal(typeof(DailyJob), ctx.Registry.Find(DailyJob.JobName)?.JobType);
        Assert.Null(ctx.Registry.Find("unknown-job"));
        Assert.Null(ctx.Registry.Find("Daily-Job"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Daily")]
    [InlineData("has space")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--dash")]
    [InlineData("under_score")]
    public void Jobs_InvalidName_Throws(string name)
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<ConfigurableJob>());
        ctx.Provider.GetRequiredService<ConfigurableJobSettings>().Name = name;

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.Registry.Jobs);
        Assert.Contains("kebab-case", ex.Message);
    }

    [Fact]
    public void Jobs_NullName_Throws()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<ConfigurableJob>());
        ctx.Provider.GetRequiredService<ConfigurableJobSettings>().Name = null;

        Assert.Throws<InvalidOperationException>(() => ctx.Registry.Jobs);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Jobs_NameLength_LimitIs100(int length, bool valid)
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<ConfigurableJob>());
        ctx.Provider.GetRequiredService<ConfigurableJobSettings>().Name = new string('a', length);

        if (valid)
        {
            Assert.Single(ctx.Registry.Jobs);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => ctx.Registry.Jobs);
        }
    }

    [Fact]
    public void Jobs_DuplicateNames_ThrowsNamingBothTypes()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<DailyJob>().AddRecurringJob<ConfigurableJob>());
        ctx.Provider.GetRequiredService<ConfigurableJobSettings>().Name = DailyJob.JobName;

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.Registry.Jobs);
        Assert.Contains(nameof(DailyJob), ex.Message);
        Assert.Contains(nameof(ConfigurableJob), ex.Message);
    }

    [Fact]
    public void Jobs_NullSchedule_Throws()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<ConfigurableJob>());
        ctx.Provider.GetRequiredService<ConfigurableJobSettings>().Schedule = null;

        var ex = Assert.Throws<InvalidOperationException>(() => ctx.Registry.Jobs);
        Assert.Contains("no schedule", ex.Message);
    }
}
