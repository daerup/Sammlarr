using Domain.Providers;
using Microsoft.Extensions.Configuration;
using Shared.Options;

namespace E2E.Tests.Recording;

internal sealed record ProviderEndpoint(string Name, string ConfigKey, string RealBaseUrl)
{
	internal string Prefix => $"/{Name}";

	internal bool IsApp { get; init; }

	internal string? Host { get; init; }
}

internal sealed record ProviderRecording(string Name, string Section, IReadOnlyList<ProviderEndpoint> Endpoints)
{
	private static readonly IConfiguration AppSettings = new ConfigurationBuilder()
		.AddJsonFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Web", "appsettings.json")))
		.Build();

	internal static readonly IReadOnlyList<ProviderRecording> All =
	[
		.. Enum.GetValues<ApiKeyProvider>().Select(ForApiKey),
		.. Enum.GetValues<AuthProvider>().SelectMany(ForAuth),
		Build("wikidata", WikidataOptions.SectionName, "BaseUrl"),
		Build("tvmaze", TvmazeOptions.SectionName, "BaseUrl"),
		Build("imdb-datasets", ImdbDatasetsOptions.SectionName, "BaseUrl"),
	];

	internal static readonly IReadOnlyList<ProviderEndpoint> Apps =
	[
		new("radarr", string.Empty, string.Empty) { IsApp = true },
		new("sonarr", string.Empty, string.Empty) { IsApp = true },
	];

	internal static readonly IReadOnlyList<ProviderEndpoint> AllEndpoints = [.. All.SelectMany(provider => provider.Endpoints), .. Apps];

	internal static readonly IReadOnlyList<ProviderRecording> SelfHosted = [.. All.Where(provider => provider.Section != PlexOptions.SectionName)];

	internal static readonly IReadOnlyList<ProviderEndpoint> ArrMetadata =
	[
		new("radarr-metadata", string.Empty, "https://api.radarr.video") { Host = "api.radarr.video" },
		new("sonarr-metadata", string.Empty, "https://skyhook.sonarr.tv") { Host = "skyhook.sonarr.tv" },
	];

	private static ProviderRecording ForApiKey(ApiKeyProvider provider) =>
		provider switch
		{
			ApiKeyProvider.Tmdb => Build(Slug(provider), TmdbOptions.SectionName, "BaseUrl"),
			ApiKeyProvider.Mdblist => Build(Slug(provider), MdblistOptions.SectionName, "BaseUrl"),
		};

	private static IReadOnlyList<ProviderRecording> ForAuth(AuthProvider provider) =>
		provider switch
		{
			AuthProvider.Plex => [Build(Slug(provider), PlexOptions.SectionName, "BaseUrl", "DiscoverUrl", "VodUrl", "MetadataUrl")],
			AuthProvider.Local => [],
		};

	private static ProviderRecording Build(string slug, string section, params string[] configKeys)
	{
		var endpoints = configKeys.Select(key => new ProviderEndpoint(EndpointName(slug, key), key, AppSettings[$"{section}:{key}"]!));
		return new ProviderRecording(slug, section, [.. endpoints]);
	}

	private static string EndpointName(string slug, string configKey) =>
		configKey == "BaseUrl" ? slug : $"{slug}-{configKey.Replace("Url", string.Empty).ToLowerInvariant()}";

	private static string Slug<T>(T provider)
		where T : struct, Enum => provider.ToString().ToLowerInvariant();
}
