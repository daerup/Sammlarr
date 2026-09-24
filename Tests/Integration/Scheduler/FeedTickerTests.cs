using Contracts.Feed;
using Contracts.Identity;
using Core.Feed.Scheduling;
using DB.Scheduling;
using Domain.Feed;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;

namespace Integration.Tests.Scheduler;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedTickerTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Nightly = "0 0 3 * * *";
	private const string Hourly = "0 0 * * * *";

	private ServiceProvider? _scheduling;

	public async ValueTask DisposeAsync()
	{
		if (_scheduling is not null)
		{
			await _scheduling.DisposeAsync();
		}

		await host.DisposeAsync();
	}

	private IFeedScheduler Testee
	{
		get
		{
			_scheduling ??= BuildScheduling();
			return _scheduling.GetRequiredService<IFeedScheduler>();
		}
	}

	[Test]
	public async Task ScheduleAsync_WritesOneTickerKeyedOnTheFeedCarryingItsOwnerAndCron()
	{
		var id = FeedId.From(Guid.NewGuid());

		await Testee.ScheduleAsync(id, new Scheduled(Nightly));

		var ticker = await SingleTickerAsync();
		await Assert.That(ticker.Id).IsEqualTo(id.Value);
		await Assert.That(ticker.Function).IsEqualTo("RunFeed");
		await Assert.That(ticker.Expression).IsEqualTo(Nightly);
		await Assert
			.That(TickerHelper.ReadTickerRequest<ScheduledFeed>(ticker.Request))
			.IsEqualTo(new ScheduledFeed(id, host.CurrentUser.Id, new FeedRunTrigger(new Scheduled(Nightly))));
	}

	[Test]
	public async Task ScheduleAsync_WhenTheCronChanges_ReplacesTheTickerInsteadOfAddingASecond()
	{
		var id = FeedId.From(Guid.NewGuid());
		await Testee.ScheduleAsync(id, new Scheduled(Nightly));

		await Testee.ScheduleAsync(id, new Scheduled(Hourly));

		await Assert.That((await SingleTickerAsync()).Expression).IsEqualTo(Hourly);
	}

	[Test]
	public async Task UnscheduleAsync_RemovesTheTicker()
	{
		var id = FeedId.From(Guid.NewGuid());
		await Testee.ScheduleAsync(id, new Scheduled(Nightly));

		await Testee.UnscheduleAsync(id);

		await Assert.That(await TickersAsync()).IsEmpty();
	}

	[Test]
	public async Task UnscheduleAsync_WhenTheFeedWasNeverScheduled_DoesNothing()
	{
		await Testee.UnscheduleAsync(FeedId.From(Guid.NewGuid()));

		await Assert.That(await TickersAsync()).IsEmpty();
	}

	private async Task<CronTickerEntity> SingleTickerAsync() => (await TickersAsync()).Single();

	private async Task<IReadOnlyList<CronTickerEntity>> TickersAsync()
	{
		await using var context = host.ContextFactory.CreateDbContext();
		return await context.Set<CronTickerEntity>().AsNoTracking().ToListAsync();
	}

	private ServiceProvider BuildScheduling()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<ICurrentUser>(host.CurrentUser);
		services.AddSammlarrScheduling(host.Connection);
		services.AddModule<FeedSchedulingModule>();
		TickerFunctionProvider.Build();
		return services.BuildServiceProvider();
	}
}
