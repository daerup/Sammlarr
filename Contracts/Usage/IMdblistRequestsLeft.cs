namespace Contracts.Usage;

public interface IMdblistRequestsLeft
{
	Task<Optional<long>> TodayAsync(CancellationToken cancellationToken = default);
}
