using Domain.Feed;
using Domain.Providers;
using Domain.Usage;

namespace Contracts.Usage;

public sealed record UsageCalls(RemoteService Service, DateTimeOffset Hour, UsageCause Cause, UsageOutcome Outcome, Optional<FeedId> Feed, long Calls);

public sealed record UsageAttempts(
	RemoteService Service,
	DateTimeOffset Hour,
	UsageCause Cause,
	Optional<FeedId> Feed,
	long Attempts,
	IReadOnlyDictionary<int, long> Buckets
);

public sealed record UsageRows(IReadOnlyList<UsageCalls> Calls, IReadOnlyList<UsageAttempts> Attempts);

public interface IUsageStore
{
	Task AddAsync(UsageRows rows, CancellationToken cancellationToken = default);

	Task<UsageRows> ReadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

	Task PruneAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}

public interface IUsageLastCalls
{
	Optional<DateTimeOffset> Of(RemoteService service);

	void Saw(RemoteService service, DateTimeOffset at);
}
