using Domain.Usage;

namespace Contracts.Usage;

public interface IMdblistRequestsLeft
{
	Task<Optional<QuotaToday>> TodayAsync(CancellationToken cancellationToken = default);
}
