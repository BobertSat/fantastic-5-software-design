using System.Net;
using System.Net.Http.Json;
using EuroGlobe.Api.Controllers.Jobs;
using EuroGlobe.Jobs.Tests.Api;
using EuroGlobe.Jobs.Tests.TestDoubles;

namespace EuroGlobe.Jobs.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public sealed class JobHistoryPersistenceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task RunHistory_SurvivesAppRestart()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        JobRunResponse? run;

        await using (var firstStart = new ApiFactory(overrides: ("ConnectionStrings:EuroGlobe", connectionString)))
        {
            var response = await firstStart.CreateClient().PostAsync($"/api/admin/jobs/{MinuteJob.JobName}/run", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            run = await response.Content.ReadFromJsonAsync<JobRunResponse>();
        }

        await using var secondStart = new ApiFactory(overrides: ("ConnectionStrings:EuroGlobe", connectionString));
        var overview = await secondStart.CreateClient().GetFromJsonAsync<JobsOverviewResponse>("/api/admin/jobs");

        Assert.True(overview!.PersistentHistory);
        var stored = Assert.Single(overview.Jobs[0].RecentRuns);
        Assert.Equal(run!.RunId, stored.RunId);
        Assert.Equal("Succeeded", stored.Status);
    }

    [Fact]
    public async Task RunNow_TwoAppInstancesOnOneDatabase_SecondIsRefusedWhileFirstRuns()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var instanceA = new ApiFactory(overrides: ("ConnectionStrings:EuroGlobe", connectionString));
        await using var instanceB = new ApiFactory(overrides: ("ConnectionStrings:EuroGlobe", connectionString));
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        instanceA.Probe.Behaviors[MinuteJob.JobName] = async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
        };

        var first = instanceA.CreateClient().PostAsync($"/api/admin/jobs/{MinuteJob.JobName}/run", null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await instanceB.CreateClient().PostAsync($"/api/admin/jobs/{MinuteJob.JobName}/run", null);
        release.SetResult();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await ApiFactory.ReadProblemAsync(second);
        Assert.Equal("JOB_ALREADY_RUNNING", problem.GetProperty("code").GetString());
        Assert.Contains("Another app instance", problem.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
        Assert.Equal(0, instanceB.Probe.Count(MinuteJob.JobName));
    }
}
