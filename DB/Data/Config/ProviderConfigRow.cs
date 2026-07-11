using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;
using Domain.Settings;

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
			TrendingProvider = TrendingProvider.Tmdb,
			SearchProvider = SearchProvider.Plex,
			RatingProvider = RatingProvider.Tmdb,
		};

	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public Guid Id { get; set; } = SingletonId;

	[Required]
	public required WatchlistProvider WatchlistProvider { get; set; }

	[Required]
	public required TrendingProvider TrendingProvider { get; set; }

	[Required]
	public required SearchProvider SearchProvider { get; set; }

	[Required]
	public required RatingProvider RatingProvider { get; set; }
}
