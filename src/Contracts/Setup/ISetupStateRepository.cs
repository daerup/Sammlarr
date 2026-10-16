namespace Contracts.Setup;

public interface ISetupStateRepository
{
	Task<Optional<DateTimeOffset>> CompletedAtAsync(CancellationToken cancellationToken = default);

	Task SetCompletedAtAsync(DateTimeOffset completedAt, CancellationToken cancellationToken = default);
}
