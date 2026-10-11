using Core.Housekeeping;
using DB.Scheduling;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Integration.Tests.Scheduler;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class HousekeepingScheduleTests(SammlarrTestHost host) : IAsyncDisposable
{
	private ServiceProvider? _scheduling;

	private ServiceProvider Scheduling => _scheduling ??= BuildScheduling();

	private HousekeepingSchedule Testee => new(Scheduling, NullLogger<HousekeepingSchedule>.Instance);

	public async ValueTask DisposeAsync()
	{
		if (_scheduling is not null)
		{
			await _scheduling.DisposeAsync();
		}

		await host.DisposeAsync();
	}

	[Test]
	public async Task StartedAsync_OverAnEarlierNightlyPass_ReplacesItWithOneAtThree()
	{
		await Scheduling
			.GetRequiredService<ICronTickerManager<CronTickerEntity>>()
			.AddAsync(
				new CronTickerEntity
				{
					Id = HousekeepingSchedule.Id,
					Function = HousekeepingTicker.FunctionName,
					Expression = "0 0 4 * * *",
				}
			);

		await Testee.StartedAsync(CancellationToken.None);

		var nightly = (await TickersAsync()).Single();
		await Assert.That(nightly.Id).IsEqualTo(HousekeepingSchedule.Id);
		await Assert.That(nightly.Function).IsEqualTo(HousekeepingTicker.FunctionName);
		await Assert.That(nightly.Expression).IsEqualTo("0 0 3 * * *");
	}

	private async Task<IReadOnlyList<CronTickerEntity>> TickersAsync()
	{
		await using var context = host.ContextFactory.CreateDbContext();
		return await context.Set<CronTickerEntity>().AsNoTracking().ToListAsync();
	}

	private ServiceProvider BuildScheduling()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSammlarrScheduling(host.Connection);
		TickerFunctionProvider.Build();
		return services.BuildServiceProvider();
	}
}
