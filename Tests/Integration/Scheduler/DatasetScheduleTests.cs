using Contracts;
using Contracts.Datasets;
using Contracts.Identity;
using Core.Datasets;
using DB.Scheduling;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;

namespace Integration.Tests.Scheduler;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class DatasetScheduleTests(SammlarrTestHost host) : IAsyncDisposable
{
	private ServiceProvider? _scheduling;

	public async ValueTask DisposeAsync()
	{
		if (_scheduling is not null)
		{
			await _scheduling.DisposeAsync();
		}

		await host.DisposeAsync();
	}

	[Test]
	public async Task StartedAsync_BeforeTheFirstLoad_SchedulesTheDailyLoadAndOneNow()
	{
		await Testee().StartedAsync(CancellationToken.None);

		var daily = (await TickersAsync<CronTickerEntity>()).Single();
		await Assert.That(daily.Function).IsEqualTo(DatasetTicker.FunctionName);
		await Assert.That(daily.Expression).IsEqualTo(DatasetSchedule.Daily);
		await Assert.That((await TickersAsync<TimeTickerEntity>()).Select(ticker => ticker.Function)).IsEquivalentTo([DatasetTicker.FunctionName]);
	}

	[Test]
	public async Task StartedAsync_OnEveryStart_KeepsOneDailyLoadAndAfterALoadNoneNow()
	{
		await host.Resolve<IDatasetLoads>().RecordAsync(new DatasetLoad(DatasetNames.ImdbRatings, DateTimeOffset.UnixEpoch, new None(), 1));

		await Testee().StartedAsync(CancellationToken.None);
		await Testee().StartedAsync(CancellationToken.None);

		await Assert.That(await TickersAsync<CronTickerEntity>()).Count().IsEqualTo(1);
		await Assert.That(await TickersAsync<TimeTickerEntity>()).IsEmpty();
	}

	private DatasetSchedule Testee()
	{
		_scheduling ??= BuildScheduling();
		return new DatasetSchedule(_scheduling, _scheduling.GetRequiredService<IDateTimeProvider>(), NullLogger<DatasetSchedule>.Instance);
	}

	private async Task<IReadOnlyList<TTicker>> TickersAsync<TTicker>()
		where TTicker : class
	{
		await using var context = host.ContextFactory.CreateDbContext();
		return await context.Set<TTicker>().AsNoTracking().ToListAsync();
	}

	private ServiceProvider BuildScheduling()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<ICurrentUser>(host.CurrentUser);
		services.AddSingleton(host.Resolve<IDateTimeProvider>());
		services.AddSingleton(host.Resolve<IDatasetLoads>());
		services.AddSingleton<IDatasetLoader>(new Named(DatasetNames.ImdbRatings));
		services.AddSammlarrScheduling(host.Connection);
		TickerFunctionProvider.Build();
		return services.BuildServiceProvider();
	}

	private sealed class Named(string name) : IDatasetLoader
	{
		public string Name => name;

		public Task<Result<int>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<Result<int>>(Error.From("Never loaded here."));
	}
}
