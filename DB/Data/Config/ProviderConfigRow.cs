using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DB.Data.Config;

[Table("ProviderConfig")]
internal record ProviderConfigRow
{
	public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

	public static ProviderConfigRow Default =>
		new()
		{
			Id = SingletonId,
			WatchlistProvider = WatchlistProvider.Plex,
			TrendingProvider = MediaRecommendationProvider.Tmdb,
			SearchProvider = MediaSearchProvider.Plex,
			RatingProvider = RatingProvider.Tmdb,
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

	[Required]
	public required RatingProvider RatingProvider { get; set; }
}
