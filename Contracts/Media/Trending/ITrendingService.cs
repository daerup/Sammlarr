using Domain.Media;

namespace Contracts.Media.Trending;

public interface ITrendingService
{
	Task<PagedResult<MediaLookup>> GetTrendingAsync(int page, CancellationToken cancellationToken = default);
}
