using Domain.Providers;
using Domain.Usage;

namespace Contracts.Usage;

public sealed record UsageSlot(DateTimeOffset At, UsageTally Tally);

public sealed record UsageServiceLine(RemoteService Service, UsageTally Tally, Optional<DateTimeOffset> LastCall, Optional<DateTimeOffset> FailingSince);

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
