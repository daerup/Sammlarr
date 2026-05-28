using Contracts.Auth;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;

namespace Contracts.Settings;

public record ProviderConfig
{
	public required WatchlistProvider WatchlistProvider { get; init; }

	public required MediaRecommendationProvider TrendingProvider { get; init; }

	public required MediaSearchProvider SearchProvider { get; init; }

	public required RatingProvider RatingProvider { get; init; }
}
