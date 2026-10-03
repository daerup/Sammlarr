using Domain.Media;

namespace Contracts.Ratings;

public interface IRatingService
{
	Task<RatingStrategy> GetDefaultAsync(CancellationToken cancellationToken = default);

	Task StoreDefaultAsync(RatingStrategy strategy, CancellationToken cancellationToken = default);

	Task<ValueSet<RatingSource>> GetAvailableAsync(CancellationToken cancellationToken = default);
}
