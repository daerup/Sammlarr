using Domain.Feed;
using Domain.Watchlist.Sync;

namespace Domain.Jobs;

public interface IJobRequest
{
	public JobKind Kind { get; }

	public string Key { get; }
}

public readonly record struct RunFeed(FeedId Id, FeedRunTrigger Trigger) : IJobRequest
{
	public JobKind Kind => JobKind.FeedRun;
	public string Key => Id.ToString();
}

public readonly record struct SyncWatchlist(WatchlistSyncTrigger Trigger) : IJobRequest
{
	public JobKind Kind => JobKind.WatchlistSync;
	public string Key => string.Empty;
}
