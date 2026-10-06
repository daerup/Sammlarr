using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist.Arr;

namespace DB.Data.Config;

[Table("ProviderConfig")]
internal record ProviderConfigRow
{
	public const int SingletonId = 1;

	public int Id { get; set; } = SingletonId;

	public required SinkKind Sink { get; set; }

	public required TrendingProvider TrendingProvider { get; set; }

	public required SearchProvider SearchProvider { get; set; }

	public required AuthProvider AuthProvider { get; set; }

	public required RegistrationMode RegistrationMode { get; set; }

	public DateTimeOffset? OnboardingCompletedAt { get; set; }

	public ArrInstanceRow Radarr { get; set; } = new();

	public ArrInstanceRow Sonarr { get; set; } = new();

	[MaxLength(64)]
	public string ArrTagPrefix { get; set; } = ArrTagScheme.Default.Prefix;

	public bool ArrTagPerUser { get; set; } = ArrTagScheme.Default.PerUser;

	public ArrUserTagSource ArrTagUserSource { get; set; } = ArrTagScheme.Default.UserSource;

	public bool ArrTagPerFeed { get; set; } = ArrTagScheme.Default.PerFeed;

	public ArrFeedTagSource ArrTagFeedSource { get; set; } = ArrTagScheme.Default.FeedSource;

	public bool ArrTagPlainPrefix { get; set; } = ArrTagScheme.Default.PlainPrefix;

	public string ArrTagProtection { get; set; } = ArrTagScheme.Default.Protection;

	public static ProviderConfigRow Default =>
		new()
		{
			Sink = SinkKind.Plex,
			TrendingProvider = TrendingProvider.Tmdb,
			SearchProvider = SearchProvider.Tmdb,
			AuthProvider = AuthProvider.Plex,
			RegistrationMode = RegistrationMode.Open,
		};
}
