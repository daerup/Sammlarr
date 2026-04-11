using Contracts.Auth;
using Contracts.Media.Search;
using Contracts.Media.Trending;

namespace Contracts.Settings;

public record ProviderSettings
{
	public required WatchlistProvider WatchlistProvider { get; init; }

	public required MediaRecommendationProvider TrendingProvider { get; init; }

	public required MediaSearchProvider SearchProvider { get; init; }
}
