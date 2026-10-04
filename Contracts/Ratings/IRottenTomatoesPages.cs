using Domain.Media;

namespace Contracts.Ratings;

public interface IRottenTomatoesPages
{
	Task<Optional<string>> FindPathAsync(MediaId id, CancellationToken cancellationToken = default);
}
