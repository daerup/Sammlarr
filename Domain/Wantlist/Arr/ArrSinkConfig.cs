namespace Domain.Wantlist.Arr;

public sealed record ArrInstanceConfig
{
	public static ArrInstanceConfig Empty { get; } = new();

	public Optional<string> Url { get; init; } = new None();

	public Optional<string> ApiKey { get; init; } = new None();

	public Optional<int> QualityProfileId { get; init; } = new None();

	public Optional<string> RootFolderPath { get; init; } = new None();

	public bool Monitored { get; init; } = true;

	public bool SearchOnAdd { get; init; } = true;

	public bool IsConnectable => Url.IsSome() && ApiKey.IsSome();

	public bool IsSetUp => IsConnectable && QualityProfileId.IsSome() && RootFolderPath.IsSome();
}

public sealed record ArrSinkConfig(ArrInstanceConfig Radarr, ArrInstanceConfig Sonarr, ArrTagScheme Tags)
{
	public static ArrSinkConfig Empty { get; } = new(ArrInstanceConfig.Empty, ArrInstanceConfig.Empty, ArrTagScheme.Default);

	public bool IsConnectable => Radarr.IsConnectable && Sonarr.IsConnectable;

	public bool IsSetUp => Radarr.IsSetUp && Sonarr.IsSetUp;

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
