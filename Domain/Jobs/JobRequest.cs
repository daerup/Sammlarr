using Domain.Feed;

namespace Domain.Jobs;

public union JobRequest(RunFeed, SyncWatchlist)
{
	public JobKind Kind =>
		this switch
		{
			RunFeed => JobKind.FeedRun,
			SyncWatchlist => JobKind.WatchlistSync,
		};
}

public readonly record struct RunFeed(FeedId Id);

public readonly record struct SyncWatchlist;
