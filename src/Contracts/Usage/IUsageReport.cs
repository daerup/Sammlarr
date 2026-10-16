using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;

namespace Contracts.Usage;

public sealed record UsageSlot(DateTimeOffset At, UsageTally Tally, IReadOnlyDictionary<UsageCause, long> Causes);

public sealed record UsageRightNow(
	Optional<DateTimeOffset> LastCall,
	Optional<DateTimeOffset> FailingSince,
	Optional<DateTimeOffset> PausedUntil,
	Optional<long> RequestsLeft,
	Optional<DateTimeOffset> DownloadedAt
);

public sealed record UsageServiceLine(
	RemoteService Service,
	UsageTally Tally,
	UsageRightNow RightNow,
	IReadOnlyList<UsageSlot> Slots,
	IReadOnlyList<UsageCauseLine> Causes
);

public sealed record UsageFeedName(FeedId Feed, string Name, FeedProvider Provider, Optional<DateTimeOffset> DeletedAt);

public sealed record UsageCauseFeed(Optional<UsageFeedName> Feed, long Calls);

public sealed record UsageCauseLine(UsageCause Cause, UsageTally Tally, IReadOnlyList<UsageCauseFeed> Feeds);

public sealed record UsagePeriod(
	UsageWindow Window,
	UsageTally Tally,
	IReadOnlyList<UsageSlot> Slots,
	IReadOnlyList<UsageServiceLine> Services,
	IReadOnlyList<UsageCauseLine> Causes
);

public sealed record UsageRun(
	RunId Run,
	DateTimeOffset StartedAt,
	TimeSpan Took,
	FeedRunTrigger Trigger,
	int Titles,
	int Added,
	int Removed,
	int NotFound,
	int CouldNotLookUp,
	UsageTally Calls,
	IReadOnlyDictionary<RemoteService, UsageTally> Services
);

public sealed record UsageFeedStats(
	long Calls,
	double CallsPerRun,
	double CallsPerTitle,
	double Unmatched,
	double UnmatchedShare,
	long CouldNotLookUp,
	TimeSpan Took
);

public sealed record UsageFeedLine(
	FeedId Feed,
	string Name,
	FeedProvider Provider,
	bool Enabled,
	Optional<DateTimeOffset> LastRun,
	IReadOnlyList<UsageRun> Runs,
	UsageFeedStats Stats,
	IReadOnlyDictionary<RemoteService, UsageFeedStats> Services
)
{
	public Optional<UsageFeedGone> Gone { get; init; } = new None();
}

public sealed record UsageFeedGone(DateTimeOffset DeletedAt, IReadOnlyDictionary<RemoteService, UsageTally> Calls);

public sealed record UsageQuota(RemoteService Service, QuotaToday Today, Optional<QuotaDay> Day, IReadOnlyList<QuotaDayUse> Days);

public sealed record UsageSnapshot(UsagePeriod Current, UsagePeriod Previous, IReadOnlyList<UsageFeedLine> Feeds, IReadOnlyList<UsageQuota> Quotas);

public interface IUsageReport
{
	Task<UsageSnapshot> ReadAsync(UsageRange range, CancellationToken cancellationToken = default);
}
