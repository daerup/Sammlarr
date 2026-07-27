using System.ComponentModel.DataAnnotations.Schema;
using DB.Data;
using Domain.Providers;

namespace DB.Data.Config;

[Table("ProviderConfig")]
internal record ProviderConfigRow : IUserOwned
{
	public required WatchlistProvider WatchlistProvider { get; set; }

	public required TrendingProvider TrendingProvider { get; set; }

	public required SearchProvider SearchProvider { get; set; }

	public required RatingProvider RatingProvider { get; set; }

	public required AuthProvider AuthProvider { get; set; }

	public static ProviderConfigRow Default =>
		new()
		{
			WatchlistProvider = WatchlistProvider.Plex,
			TrendingProvider = TrendingProvider.Tmdb,
			SearchProvider = SearchProvider.Plex,
			RatingProvider = RatingProvider.Tmdb,
			AuthProvider = AuthProvider.Plex,
		};
}
