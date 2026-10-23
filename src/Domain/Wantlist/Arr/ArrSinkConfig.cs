namespace Domain.Wantlist.Arr;

public sealed record ArrInstanceConfig
{
	public static ArrInstanceConfig Empty { get; } = new();

	public Optional<string> Url { get; init; } = new None();

	public Optional<string> ExternalUrl { get; init; } = new None();

	public Optional<Secret> ApiKey { get; init; } = new None();

	public Optional<int> QualityProfileId { get; init; } = new None();

	public Optional<string> RootFolderPath { get; init; } = new None();

	public Optional<int> AnimeQualityProfileId { get; init; } = new None();

	public Optional<string> AnimeRootFolderPath { get; init; } = new None();

	public bool Monitored { get; init; } = true;

	public bool SearchOnAdd { get; init; } = true;

	public bool DeleteFiles { get; init; }

	public Optional<string> LinkUrl => ExternalUrl.Or(() => Url);

	public bool IsConnectable => Url.IsSome() && ApiKey.IsSome();

	public bool IsSetUp => IsConnectable && QualityProfileId.IsSome() && RootFolderPath.IsSome();

	public bool RoutesAnime => AnimeRootFolderPath.IsSome();

	public Optional<string> FolderFor(ArrRoute route)
	{
		return route switch
		{
			ArrRoute.Default => RootFolderPath,
			ArrRoute.Anime => AnimeRootFolderPath.Or(() => RootFolderPath),
		};
	}

	public Optional<int> ProfileFor(ArrRoute route)
	{
		return route switch
		{
			ArrRoute.Default => QualityProfileId,
			ArrRoute.Anime => AnimeQualityProfileId.Or(() => QualityProfileId),
		};
	}
}

public sealed record ArrSinkConfig(ArrInstanceConfig Radarr, ArrInstanceConfig Sonarr, ArrTagScheme Tags)
{
	public static ArrSinkConfig Empty { get; } = new(ArrInstanceConfig.Empty, ArrInstanceConfig.Empty, ArrTagScheme.Default);

	public bool IsConnectable => Radarr.IsConnectable && Sonarr.IsConnectable;

	public bool IsSetUp => Radarr.IsSetUp && Sonarr.IsSetUp;

	public bool RoutesAnime => Radarr.RoutesAnime || Sonarr.RoutesAnime;

	public ArrInstanceConfig Of(ArrApp app)
	{
		return app switch
		{
			ArrApp.Radarr => Radarr,
			ArrApp.Sonarr => Sonarr,
		};
	}

	public ArrSinkConfig With(ArrApp app, ArrInstanceConfig instance)
	{
		return app switch
		{
			ArrApp.Radarr => this with { Radarr = instance },
			ArrApp.Sonarr => this with { Sonarr = instance },
		};
	}
}
