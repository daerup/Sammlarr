using Domain.Media;

namespace Contracts.Media.Trending;

public interface IMediaRecommendationService
{
	Task<PagedResult<MediaLookup>> GetTrendingAsync(int page, CancellationToken cancellationToken = default);
}
