using Domain.Providers;
using Microsoft.Extensions.Configuration;
using Shared.Options;

namespace E2E.Tests.Recording;

internal sealed record ProviderEndpoint(string Name, string ConfigKey, string RealBaseUrl)
{
	internal string Prefix => $"/{Name}";
}

internal sealed record ProviderRecording(string Name, string Section, IReadOnlyList<ProviderEndpoint> Endpoints)
{
	private static readonly IConfiguration AppSettings = new ConfigurationBuilder()
		.AddJsonFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Web", "appsettings.json")))
		.Build();

	internal static readonly IReadOnlyList<ProviderRecording> All =
	[
		.. Enum.GetValues<ApiKeyProvider>().Select(ForApiKey),
		.. Enum.GetValues<AuthProvider>().Select(ForAuth),
	];

	internal static readonly IReadOnlyList<ProviderEndpoint> AllEndpoints = [.. All.SelectMany(provider => provider.Endpoints)];

	private static ProviderRecording ForApiKey(ApiKeyProvider provider) =>
		provider switch
		{
			ApiKeyProvider.Tmdb => Build(provider, TmdbOptions.SectionName, "BaseUrl"),
			ApiKeyProvider.Mdblist => Build(provider, MdblistOptions.SectionName, "BaseUrl"),
		};

	private static ProviderRecording ForAuth(AuthProvider provider) =>
		provider switch
		{
			AuthProvider.Plex => Build(provider, PlexOptions.SectionName, "BaseUrl", "DiscoverUrl", "VodUrl", "MetadataUrl"),
		};

	private static ProviderRecording Build<T>(T provider, string section, params string[] configKeys)
		where T : struct, Enum
	{
		var slug = Slug(provider);
		var endpoints = configKeys.Select(key => new ProviderEndpoint(EndpointName(slug, key), key, AppSettings[$"{section}:{key}"]!));
		return new ProviderRecording(slug, section, [.. endpoints]);
	}

	private static string EndpointName(string slug, string configKey) =>
		configKey == "BaseUrl" ? slug : $"{slug}-{configKey.Replace("Url", string.Empty).ToLowerInvariant()}";

	private static string Slug<T>(T provider)
		where T : struct, Enum => provider.ToString().ToLowerInvariant();
}
