using Domain.Providers;

namespace Domain.Settings;

public record ProviderConfig
{
	public required WatchlistProvider WatchlistProvider { get; init; }
	public required TrendingProvider TrendingProvider { get; init; }
	public required SearchProvider SearchProvider { get; init; }
	public required RatingProvider RatingProvider { get; init; }
}
