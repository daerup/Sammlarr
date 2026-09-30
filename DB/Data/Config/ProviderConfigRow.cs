using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;
using Domain.Settings;

namespace DB.Data.Config;

[Table("ProviderConfig")]
internal record ProviderConfigRow
{
	public const int SingletonId = 1;

	public int Id { get; set; } = SingletonId;

	public required SinkKind Sink { get; set; }

	public required TrendingProvider TrendingProvider { get; set; }

	public required SearchProvider SearchProvider { get; set; }

	public required RatingProvider RatingProvider { get; set; }

	public required AuthProvider AuthProvider { get; set; }

	public required RegistrationMode RegistrationMode { get; set; }

	public static ProviderConfigRow Default =>
		new()
		{
			Sink = SinkKind.Plex,
			TrendingProvider = TrendingProvider.Tmdb,
			SearchProvider = SearchProvider.Plex,
			RatingProvider = RatingProvider.Tmdb,
			AuthProvider = AuthProvider.Plex,
			RegistrationMode = RegistrationMode.Open,
		};
}
