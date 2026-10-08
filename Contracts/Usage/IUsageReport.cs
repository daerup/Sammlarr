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

public sealed record UsageSnapshot(UsagePeriod Current, UsagePeriod Previous);

public interface IUsageReport
{
	Task<UsageSnapshot> ReadAsync(UsageRange range, CancellationToken cancellationToken = default);
}
