using Contracts.Media.Trending;

namespace Contracts.Media;

public interface IMediaRecommendationService
{
	Task<TrendingPageResult> GetTrendingAsync(int page, CancellationToken cancellationToken = default);
}
