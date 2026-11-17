using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Tests.TestDoubles;

namespace EuroGlobe.Jobs.Tests.Contracts;

public sealed class JobContextTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 2, 0, 0, TimeSpan.Zero);
    private static readonly FakeClock Clock = new(Now);

    private static JobContext Create() => new(Guid.NewGuid(), "mortgage-repayments", Now, JobTrigger.Scheduled, Clock);

    [Fact]
    public void Constructor_ValidArguments_ExposesThem()
    {
        var runId = Guid.NewGuid();

        var context = new JobContext(runId, "leasing-billing", Now, JobTrigger.Manual, Clock);

        Assert.Equal(runId, context.RunId);
        Assert.Equal("leasing-billing", context.JobName);
        Assert.Equal(Now, context.ScheduledFor);
        Assert.Equal(JobTrigger.Manual, context.Trigger);
        Assert.Same(Clock, context.Clock);
        Assert.Equal(0, context.ItemsProcessed);
        Assert.Equal(0, context.ItemsFailed);
    }

    [Fact]
    public void Constructor_EmptyRunId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new JobContext(Guid.Empty, "job", Now, JobTrigger.Scheduled, Clock));
        Assert.Equal("runId", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankJobName_Throws(string jobName)
    {
        Assert.Throws<ArgumentException>(() => new JobContext(Guid.NewGuid(), jobName, Now, JobTrigger.Scheduled, Clock));
    }

    [Fact]
    public void Constructor_NullJobName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new JobContext(Guid.NewGuid(), null!, Now, JobTrigger.Scheduled, Clock));
    }

    [Fact]
    public void Constructor_NullClock_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new JobContext(Guid.NewGuid(), "job", Now, JobTrigger.Scheduled, null!));
    }

    [Fact]
    public void ReportProcessed_DefaultAndExplicitCounts_Accumulate()
    {
        var context = Create();

        context.ReportProcessed();
        context.ReportProcessed(4);
        context.ReportProcessed(0);

        Assert.Equal(5, context.ItemsProcessed);
        Assert.Equal(0, context.ItemsFailed);
    }

    [Fact]
    public void ReportFailed_DefaultAndExplicitCounts_Accumulate()
    {
        var context = Create();

        context.ReportFailed();
        context.ReportFailed(2);

        Assert.Equal(3, context.ItemsFailed);
        Assert.Equal(0, context.ItemsProcessed);
    }

    [Fact]
    public void ReportProcessed_NegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create().ReportProcessed(-1));
    }

    [Fact]
    public void ReportFailed_NegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create().ReportFailed(-1));
    }

    [Fact]
    public async Task ReportProcessed_ConcurrentCalls_CountsEveryCall()
    {
        var context = Create();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                context.ReportProcessed();
            }
        })));

        Assert.Equal(8_000, context.ItemsProcessed);
    }
}
