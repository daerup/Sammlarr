using Contracts.Datasets;
using Contracts.Media;
using Domain.Providers;

namespace Contracts.Housekeeping;

public interface IHousekeepingTasks
{
	event Action Changed;

	Task<HousekeepingOverview> OverviewAsync(CancellationToken cancellationToken = default);

	Task<bool> RunPassAsync(CancellationToken cancellationToken = default);

	Task<bool> LoadDatasetAsync(RemoteService dataset, CancellationToken cancellationToken = default);
}

public sealed record HousekeepingOverview(
	DatabaseSize Size,
	PassTask Pass,
	IReadOnlyList<DatasetTask> Datasets,
	CachedTitles Cache,
	StoredTwiceCount StoredTwice
);

public readonly record struct StoredTwiceCount(int Undecided, int KeptApart);

public sealed record PassTask(Optional<DateTimeOffset> NextRun, Optional<TaskRun> Running, Optional<HousekeepingReport> Last);

public sealed record DatasetTask(
	RemoteService Service,
	Optional<DateTimeOffset> NextRun,
	Optional<TaskRun> Running,
	Optional<DatasetCheck> Last,
	Optional<DatasetLoad> Loaded
);

public sealed record TaskRun(DateTimeOffset Since, bool Started);
