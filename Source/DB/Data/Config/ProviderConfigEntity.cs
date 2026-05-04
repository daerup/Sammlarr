using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Auth;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using DB.Conversion.Enums;

namespace DB.Data.Config;

[Table("ProviderConfig")]
internal record ProviderConfigEntity
{
	public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

	public static ProviderConfigEntity Default =>
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
	[EnumAsString]
	public required WatchlistProvider WatchlistProvider { get; set; }

	[Required]
	[EnumAsString]
	public required MediaRecommendationProvider TrendingProvider { get; set; }

	[Required]
	[EnumAsString]
	public required MediaSearchProvider SearchProvider { get; set; }

	[Required]
	[EnumAsString]
	public required RatingProvider RatingProvider { get; set; }
}
