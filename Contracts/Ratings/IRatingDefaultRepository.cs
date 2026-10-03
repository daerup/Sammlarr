using Domain.Media;

namespace Contracts.Ratings;

public interface IRatingDefaultRepository
{
	Task<Optional<RatingStrategy>> FindAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(RatingStrategy strategy, CancellationToken cancellationToken = default);
}
