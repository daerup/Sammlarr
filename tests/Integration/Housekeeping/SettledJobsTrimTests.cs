using Contracts.Housekeeping;
using Domain.Jobs;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SettledJobsTrimTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IHousekeepingStep Testee => host.Step(HousekeepingStep.SettledJobs);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForJobsSettledOver30DaysAgo_RemovesThemAndKeepsRecentAndQueuedOnes()
	{
		await host.SeedJobAsync(JobState.Done, Now.AddDays(-31));
		await host.SeedJobAsync(JobState.Failed, Now.AddDays(-40));
		var recent = await host.SeedJobAsync(JobState.Done, Now.AddDays(-29));
		var queued = await host.SeedJobAsync(JobState.Queued, Now.AddDays(-60));

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(2);
		await Assert.That(await ctx.Jobs.Select(job => job.Id).ToListAsync()).IsEquivalentTo([recent, queued]);
	}
}
