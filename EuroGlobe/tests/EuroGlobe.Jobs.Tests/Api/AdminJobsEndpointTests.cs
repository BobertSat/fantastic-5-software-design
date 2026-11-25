using System.Net;
using System.Net.Http.Json;
using EuroGlobe.Api.Controllers.Jobs;
using EuroGlobe.Jobs.Tests.TestDoubles;

namespace EuroGlobe.Jobs.Tests.Api;

public sealed class AdminJobsEndpointTests
{
    [Fact]
    public async Task GetJobs_ListsRegisteredJobsWithScheduleAndRuntimeFlags()
    {
        await using var api = new ApiFactory();
        var client = api.CreateClient();

        var overview = await client.GetFromJsonAsync<JobsOverviewResponse>("/api/admin/jobs");

        Assert.NotNull(overview);
        Assert.False(overview.SchedulerEnabled);
        Assert.False(overview.PersistentHistory);
        Assert.True(overview.SimulatedClock);
        var job = Assert.Single(overview.Jobs);
        Assert.Equal(MinuteJob.JobName, job.Name);
        Assert.Equal("every 00:01:00", job.Schedule);
        Assert.InRange(job.NextOccurrence - overview.UtcNow, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        Assert.Empty(job.RecentRuns);
    }

    [Fact]
    public async Task RunNow_KnownJob_RunsItAndShowsItInRecentRuns()
    {
        await using var api = new ApiFactory();
        var client = api.CreateClient();

        var response = await client.PostAsync($"/api/admin/jobs/{MinuteJob.JobName}/run", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JobRunResponse>();
        Assert.NotNull(run);
        Assert.Equal("Succeeded", run.Status);
        Assert.Equal("Manual", run.Trigger);
        Assert.Equal(1, api.Probe.Count(MinuteJob.JobName));

        var overview = await client.GetFromJsonAsync<JobsOverviewResponse>("/api/admin/jobs");
        Assert.Equal(run.RunId, Assert.Single(overview!.Jobs[0].RecentRuns).RunId);
    }

    [Fact]
    public async Task RunNow_UnknownJob_Returns404ProblemWithCode()
    {
        await using var api = new ApiFactory();

        var response = await api.CreateClient().PostAsync("/api/admin/jobs/no-such-job/run", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ApiFactory.ReadProblemAsync(response);
        Assert.Equal("JOB_NOT_FOUND", problem.GetProperty("code").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task RunNow_JobAlreadyRunning_Returns409()
    {
        await using var api = new ApiFactory();
        var client = api.CreateClient();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        api.Probe.Behaviors[MinuteJob.JobName] = async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
        };

        var first = client.PostAsync($"/api/admin/jobs/{MinuteJob.JobName}/run", null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await client.PostAsync($"/api/admin/jobs/{MinuteJob.JobName}/run", null);
        release.SetResult();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("JOB_ALREADY_RUNNING", (await ApiFactory.ReadProblemAsync(second)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
    }

    [Fact]
    public async Task RunNow_JobFails_Returns200WithFailedRun()
    {
        await using var api = new ApiFactory();
        api.Probe.Behaviors[MinuteJob.JobName] = (_, _) => throw new InvalidOperationException("boom");

        var response = await api.CreateClient().PostAsync($"/api/admin/jobs/{MinuteJob.JobName}/run", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JobRunResponse>();
        Assert.Equal("Failed", run!.Status);
        Assert.Equal("InvalidOperationException: boom", run.Error);
    }
}
