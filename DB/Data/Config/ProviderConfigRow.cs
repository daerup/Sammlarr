using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DB.Conversion.Encryption;
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

	[MaxLength(2048)]
	public string? RadarrUrl { get; set; }

	[MaxLength(4096)]
	[Encrypted]
	public string? RadarrApiKey { get; set; }

	public int? RadarrQualityProfileId { get; set; }

	[MaxLength(1024)]
	public string? RadarrRootFolderPath { get; set; }

	public bool RadarrMonitored { get; set; } = true;

	public bool RadarrSearchOnAdd { get; set; } = true;

	[MaxLength(2048)]
	public string? SonarrUrl { get; set; }

	[MaxLength(4096)]
	[Encrypted]
	public string? SonarrApiKey { get; set; }

	public int? SonarrQualityProfileId { get; set; }

	[MaxLength(1024)]
	public string? SonarrRootFolderPath { get; set; }

	public bool SonarrMonitored { get; set; } = true;

	public bool SonarrSearchOnAdd { get; set; } = true;

	[MaxLength(64)]
	public string ArrTagPrefix { get; set; } = ArrTagScheme.Default.Prefix;

	public bool ArrTagPerUser { get; set; } = ArrTagScheme.Default.PerUser;

	public ArrUserTagSource ArrTagUserSource { get; set; } = ArrTagScheme.Default.UserSource;

	public bool ArrTagPerFeed { get; set; } = ArrTagScheme.Default.PerFeed;

	public ArrFeedTagSource ArrTagFeedSource { get; set; } = ArrTagScheme.Default.FeedSource;

	public bool ArrTagPlainPrefix { get; set; } = ArrTagScheme.Default.PlainPrefix;

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
