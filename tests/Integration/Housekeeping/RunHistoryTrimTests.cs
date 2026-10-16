using Contracts.Housekeeping;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RunHistoryTrimTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IHousekeepingStep Testee => host.Step(HousekeepingStep.RunHistory);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForARunPastNinetyDays_RemovesItsTitlesCallsAndTimesButKeepsTheRunAndItsCounts()
	{
		var feed = await host.SeedFeedAsync();
		var old = await host.SeedRunAsync(feed, Now.AddDays(-91), Guid.NewGuid());
		var recent = await host.SeedRunAsync(feed, Now.AddDays(-89), Guid.NewGuid());

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(3);
		await Assert.That(await ctx.FeedRunItems.Select(item => item.FeedRunId).ToListAsync()).IsEquivalentTo([recent]);
		await Assert.That(await ctx.FeedRunCalls.Select(call => call.FeedRunId).ToListAsync()).IsEquivalentTo([recent]);
		await Assert.That(await ctx.FeedRunTimes.Select(time => time.FeedRunId).ToListAsync()).IsEquivalentTo([recent]);
		await Assert.That(await ctx.FeedRuns.Where(run => run.Id == old).Select(run => run.Added).SingleAsync()).IsEqualTo(1);
	}
}
