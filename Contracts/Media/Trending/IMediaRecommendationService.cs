namespace Contracts.Media.Trending;

public interface IMediaRecommendationService
{
	Task<PagedResult<MediaMatch>> GetTrendingAsync(int page, CancellationToken cancellationToken = default);
}
