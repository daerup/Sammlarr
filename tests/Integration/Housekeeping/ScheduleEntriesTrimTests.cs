using Contracts.Housekeeping;
using Harness;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ScheduleEntriesTrimTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IHousekeepingStep Testee => host.Step(HousekeepingStep.ScheduleEntries);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForOccurrencesSettledOver30DaysAgo_RemovesThemAndKeepsPendingAndRecentOnes()
	{
		var ticker = await host.SeedTickerAsync();
		await host.SeedOccurrenceAsync(ticker, TickerStatus.Done, Now.AddDays(-31));
		await host.SeedOccurrenceAsync(ticker, TickerStatus.Failed, Now.AddDays(-45));
		var recent = await host.SeedOccurrenceAsync(ticker, TickerStatus.Done, Now.AddDays(-29));
		var pending = await host.SeedOccurrenceAsync(ticker, TickerStatus.Idle, Now.AddDays(-32));

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(2);
		await Assert
			.That(await ctx.Set<CronTickerOccurrenceEntity<CronTickerEntity>>().Select(occurrence => occurrence.Id).ToListAsync())
			.IsEquivalentTo([recent, pending]);
	}

	[Test]
	public async Task RunAsync_ForOneOffRunsSettledOver30DaysAgo_RemovesThemAndKeepsPendingAndRecentOnes()
	{
		await SeedOnceAsync(TickerStatus.Done, Now.AddDays(-31));
		var recent = await SeedOnceAsync(TickerStatus.Done, Now.AddDays(-29));
		var pending = await SeedOnceAsync(TickerStatus.Idle, Now.AddDays(-40));

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(1);
		await Assert.That(await ctx.Set<TimeTickerEntity>().Select(ticker => ticker.Id).ToListAsync()).IsEquivalentTo([recent, pending]);
	}

	private async Task<Guid> SeedOnceAsync(TickerStatus status, DateTimeOffset executionTime)
	{
		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		var ticker = new TimeTickerEntity
		{
			Id = Guid.NewGuid(),
			Function = "Housekeeping",
			ExecutionTime = executionTime.UtcDateTime,
		};
		ctx.Set<TimeTickerEntity>().Add(ticker);
		await ctx.SaveChangesAsync();
		await ctx.Set<TimeTickerEntity>().Where(row => row.Id == ticker.Id).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, status));
		return ticker.Id;
	}
}
