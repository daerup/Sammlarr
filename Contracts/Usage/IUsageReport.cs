using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;

namespace Contracts.Usage;

public sealed record UsageSlot(DateTimeOffset At, UsageTally Tally);

public sealed record UsageRightNow(
	Optional<DateTimeOffset> LastCall,
	Optional<DateTimeOffset> FailingSince,
	Optional<DateTimeOffset> PausedUntil,
	Optional<long> RequestsLeft,
	Optional<DateTimeOffset> DownloadedAt
);

public sealed record UsageServiceLine(RemoteService Service, UsageTally Tally, UsageRightNow RightNow);

public sealed record UsageCauseLine(UsageCause Cause, UsageTally Tally);

public sealed record UsagePeriod(
	UsageWindow Window,
	UsageTally Tally,
	IReadOnlyList<UsageSlot> Slots,
	IReadOnlyList<UsageServiceLine> Services,
	IReadOnlyList<UsageCauseLine> Causes
);

public sealed record UsageRun(RunId Run, DateTimeOffset StartedAt, TimeSpan Took, int Titles, int NotFound, int CouldNotLookUp, UsageTally Calls);

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
	UsageFeedStats Stats
);

public sealed record UsageSnapshot(UsagePeriod Current, UsagePeriod Previous, IReadOnlyList<UsageFeedLine> Feeds);

public interface IUsageReport
{
	Task<UsageSnapshot> ReadAsync(UsageRange range, CancellationToken cancellationToken = default);
}
