using Domain.Wantlist.Arr;

namespace Contracts.Settings;

public sealed record ArrInstanceConfig
{
	public static ArrInstanceConfig Empty { get; } = new();

	public Optional<string> Url { get; init; } = new None();

	[Sensitive]
	public Optional<string> ApiKey { get; init; } = new None();

	public Optional<int> QualityProfileId { get; init; } = new None();

	public Optional<string> RootFolderPath { get; init; } = new None();

	public bool Monitored { get; init; } = true;

	public bool SearchOnAdd { get; init; } = true;
}

public sealed record ArrSinkConfig(ArrInstanceConfig Radarr, ArrInstanceConfig Sonarr, ArrTagScheme Tags)
{
	public static ArrSinkConfig Empty { get; } = new(ArrInstanceConfig.Empty, ArrInstanceConfig.Empty, ArrTagScheme.Default);
}
