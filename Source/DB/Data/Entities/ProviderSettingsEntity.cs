using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Auth;
using Contracts.Media.Search;
using Contracts.Media.Trending;

namespace DB.Data.Entities;

[Table("ProviderSettings")]
public record ProviderSettingsEntity
{
	public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

	public static ProviderSettingsEntity Default =>
		new()
		{
			Id = SingletonId,
			WatchlistProvider = WatchlistProvider.Plex,
			TrendingProvider = MediaRecommendationProvider.Tmdb,
			SearchProvider = MediaSearchProvider.Plex,
		};

	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public Guid Id { get; set; } = SingletonId;

	[Required]
	public required WatchlistProvider WatchlistProvider { get; set; }

	[Required]
	public required MediaRecommendationProvider TrendingProvider { get; set; }

	[Required]
	public required MediaSearchProvider SearchProvider { get; set; }
}
