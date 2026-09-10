using Domain.Providers;
using Microsoft.Extensions.Configuration;
using Shared.Options;

namespace E2E.Tests.Recording;

internal sealed record ProviderRecording(string Name, string Section, string RealBaseUrl)
{
	private static readonly IConfiguration AppSettings = new ConfigurationBuilder()
		.AddJsonFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Web", "appsettings.json")))
		.Build();

	internal static readonly IReadOnlyList<ProviderRecording> All =
	[
		.. Enum.GetValues<ApiKeyProvider>().Select(ForApiKey),
		.. Enum.GetValues<AuthProvider>().Select(ForAuth),
	];

	internal string Prefix => $"/{Name}";

	private static ProviderRecording ForApiKey(ApiKeyProvider provider) =>
		provider switch
		{
			ApiKeyProvider.Tmdb => Build(provider, TmdbOptions.SectionName),
			ApiKeyProvider.Mdblist => Build(provider, MdblistOptions.SectionName),
		};

	private static ProviderRecording ForAuth(AuthProvider provider) =>
		provider switch
		{
			AuthProvider.Plex => Build(provider, PlexOptions.SectionName),
		};

	private static ProviderRecording Build<T>(T provider, string section)
		where T : struct, Enum => new(Slug(provider), section, AppSettings[$"{section}:BaseUrl"]!);

	private static string Slug<T>(T provider)
		where T : struct, Enum => provider.ToString().ToLowerInvariant();
}
