using Domain.Feed;
using Domain.Wantlist.Sync;

namespace Domain.Jobs;

public interface IJobRequest
{
	public JobKind Kind { get; }

	public string Key { get; }
}

public readonly record struct RunFeed(FeedId Id, FeedRunTrigger Trigger, bool SyncAfter = false) : IJobRequest
{
	public JobKind Kind => JobKind.FeedRun;
	public string Key => Id.ToString();
	public bool ArmsSync => Trigger is Scheduled || SyncAfter;
}

public readonly record struct SyncWantlist(WantlistSyncTrigger Trigger) : IJobRequest
{
	public JobKind Kind => JobKind.WantlistSync;
	public string Key => string.Empty;
}

public readonly record struct FillDetails : IJobRequest
{
	public JobKind Kind => JobKind.DetailsFill;
	public string Key => string.Empty;
}
