namespace Contracts.Media.Trending;

public interface IMediaRecommendationService
{
	Task<TrendingPageResult> GetTrendingAsync(int page, CancellationToken cancellationToken = default);
}
