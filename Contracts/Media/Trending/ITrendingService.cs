using Domain.Media;

namespace Contracts.Media.Trending;

public interface ITrendingService
{
	Task<PagedResult<MediaMatchResult>> GetTrendingAsync(int page, CancellationToken cancellationToken = default);
}
