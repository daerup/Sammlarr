using Contracts.Datasets;
using Contracts.Housekeeping;
using Contracts.Identity;
using Contracts.Media;
using Core.Datasets;
using Core.Jobs;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Core.Housekeeping;

internal sealed class HousekeepingTasks(
	IHousekeepingReports reports,
	IDatasetChecks checks,
	IDatasetLoads loads,
	IDatabaseFile database,
	IMediaCachePurger cache,
	IIdentityConflicts conflicts,
	ITimeTickerManager<TimeTickerEntity> once,
	TaskActivity activity,
	ICurrentUser currentUser,
	TimeProvider clock
) : IHousekeepingTasks
{
	public event Action Changed
	{
		add => activity.Changed += value;
		remove => activity.Changed -= value;
	}

	public async Task<HousekeepingOverview> OverviewAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		var pass = new PassTask(Next(HousekeepingSchedule.Nightly), activity.Running(HousekeepingTicker.FunctionName), await reports.LastAsync(cancellationToken));
		List<DatasetTask> datasets = [];
		foreach (var (service, name) in DatasetNames.ByService)
		{
			var last = await checks.FindAsync(name, cancellationToken);
			var loaded = await loads.FindAsync(name, cancellationToken);
			datasets.Add(new DatasetTask(service, Next(DatasetSchedule.Daily), activity.Running(name), last, loaded));
		}

		var notes = await conflicts.AllAsync(cancellationToken);
		var storedTwice = new StoredTwiceCount(notes.Count(note => note.State is Undecided), notes.Count(note => note.State is Distinct));
		return new HousekeepingOverview(await database.SizeAsync(cancellationToken), pass, datasets, await cache.CountAsync(cancellationToken), storedTwice);
	}

	public Task<bool> RunPassAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		return StartAsync(HousekeepingTicker.FunctionName, HousekeepingTicker.Once(clock.GetUtcNow()), cancellationToken);
	}

	public Task<bool> LoadDatasetAsync(RemoteService dataset, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		var name = DatasetNames.ByService[dataset];
		return StartAsync(name, DatasetTicker.Once(name, clock.GetUtcNow(), DatasetRead.Always), cancellationToken);
	}

	private async Task<bool> StartAsync(string task, TimeTickerEntity ticker, CancellationToken cancellationToken)
	{
		if (!activity.TryClaim(task))
		{
			return false;
		}

		var added = await once.AddAsync(ticker, cancellationToken);
		if (!added.IsSucceeded)
		{
			activity.Release(task);
			throw new InvalidOperationException($"{task} could not be started.", added.Exception);
		}

		return true;
	}

	private Optional<DateTimeOffset> Next(string cron) => new Scheduled(cron).NextRun(clock.GetUtcNow(), clock.LocalTimeZone);
}
