using Contracts;
using Contracts.Datasets;
using Contracts.Housekeeping;
using Contracts.Identity;
using Contracts.Media;
using Core.Datasets;
using Core.Housekeeping;
using Core.Jobs;
using DB.Scheduling;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class HousekeepingTasksTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 11, 22, 0, 0, DateTimeKind.Local));

	private ServiceProvider? _scheduling;

	private TaskActivity? _activity;

	private TaskActivity Activity => _activity ??= new TaskActivity(host.Resolve<IDateTimeProvider>());

	private HousekeepingTasks Testee
	{
		get
		{
			_scheduling ??= BuildScheduling();
			return new HousekeepingTasks(
				host.Resolve<IHousekeepingReports>(),
				host.Resolve<IDatasetChecks>(),
				host.Resolve<IDatasetLoads>(),
				host.Resolve<IDatabaseFile>(),
				host.Resolve<IMediaCachePurger>(),
				_scheduling.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>(),
				Activity,
				host.CurrentUser,
				host.Resolve<IDateTimeProvider>()
			);
		}
	}

	[Before(Test)]
	public void SetClock() => host.SetCurrentTime(Now);

	public async ValueTask DisposeAsync()
	{
		if (_scheduling is not null)
		{
			await _scheduling.DisposeAsync();
		}

		await host.DisposeAsync();
	}

	[Test]
	public async Task OverviewAsync_BeforeAnythingRan_ListsEveryDatasetWithItsNextLoadInLocalTimeAndNoLastRun()
	{
		var overview = await Testee.OverviewAsync();

		await Assert.That(overview.Pass.Last).IsNone();
		await Assert.That(overview.Datasets.Select(dataset => dataset.Service)).IsEquivalentTo([RemoteService.ImdbDatasets, RemoteService.AnimeLists]);
		await Assert.That(overview.Datasets[0].Last).IsNone();
		await Assert.That(overview.Datasets[1].Last).IsNone();
		await Assert.That(overview.Datasets[0].NextRun).IsSome().And.IsEqualTo(new DateTimeOffset(new DateTime(2026, 10, 12, 4, 30, 0, DateTimeKind.Local)));
	}

	[Test]
	public async Task OverviewAsync_AfterAPassAndADatasetCheck_ShowsBothAsTheirLastRun()
	{
		var report = new HousekeepingReport(
			Now.AddHours(-17),
			Some.Of(Now.AddHours(-17)),
			[new StepCount(HousekeepingStep.RunHistory, 500)],
			new None(),
			new None()
		);
		var check = new DatasetCheck(DatasetNames.AnimeIds, Now, Now, DatasetOutcome.Unchanged, new None());
		await host.Resolve<IHousekeepingReports>().SaveAsync(report);
		await host.Resolve<IDatasetChecks>().RecordAsync(check);

		var overview = await Testee.OverviewAsync();

		await Assert.That(overview.Pass.Last).IsSome().And.IsEquivalentTo(report);
		await Assert.That(overview.Datasets.Single(dataset => dataset.Service == RemoteService.AnimeLists).Last).IsSome().And.IsEqualTo(check);
	}

	[Test]
	public async Task RunPassAsync_WhenIdle_SchedulesThePassNowAndShowsItQueued()
	{
		var started = await Testee.RunPassAsync();

		await Assert.That(started).IsTrue();
		await Assert.That((await OnceTickersAsync()).Select(ticker => ticker.Function)).IsEquivalentTo([HousekeepingTicker.FunctionName]);
		await Assert.That((await Testee.OverviewAsync()).Pass.Running).IsSome().And.IsEqualTo(new TaskRun(Now, false));
	}

	[Test]
	public async Task RunPassAsync_WhileThePassRuns_IsRefusedAndSchedulesNothing()
	{
		using var running = Activity.Begin(HousekeepingTicker.FunctionName);

		var started = await Testee.RunPassAsync();

		await Assert.That(started).IsFalse();
		await Assert.That(await OnceTickersAsync()).IsEmpty();
	}

	[Test]
	public async Task LoadDatasetAsync_Twice_SchedulesOneLoadOfThatDatasetAndRefusesTheSecond()
	{
		var first = await Testee.LoadDatasetAsync(RemoteService.ImdbDatasets);
		var second = await Testee.LoadDatasetAsync(RemoteService.ImdbDatasets);

		await Assert.That(first).IsTrue();
		await Assert.That(second).IsFalse();
		var ticker = (await OnceTickersAsync()).Single();
		await Assert.That(ticker.Function).IsEqualTo(DatasetTicker.FunctionName);
		await Assert.That(TickerHelper.ReadTickerRequest<DatasetRequest>(ticker.Request)).IsEqualTo(new DatasetRequest(DatasetNames.ImdbRatings));
	}

	private async Task<IReadOnlyList<TimeTickerEntity>> OnceTickersAsync()
	{
		await using var context = host.ContextFactory.CreateDbContext();
		return await context.Set<TimeTickerEntity>().AsNoTracking().ToListAsync();
	}

	private ServiceProvider BuildScheduling()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<ICurrentUser>(host.CurrentUser);
		services.AddSammlarrScheduling(host.Connection);
		TickerFunctionProvider.Build();
		return services.BuildServiceProvider();
	}
}
