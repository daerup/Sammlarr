using Domain.Media;

namespace Contracts.Ratings;

public interface IRottenTomatoesPageFinder
{
	Task<Result<Optional<string>>> FindPathAsync(ImdbId imdb, CancellationToken cancellationToken = default);
}
