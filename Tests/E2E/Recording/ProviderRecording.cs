using Domain.Providers;
using Microsoft.Extensions.Configuration;
using Shared.Options;

namespace E2E.Tests.Recording;

internal sealed record ProviderRecording(string Name, string Section, string RealBaseUrl, string CredentialKey)
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

	internal string? Credential => RecordingConfiguration.Value(CredentialKey);

	internal static bool AnyCredentialPresent => All.Any(provider => !string.IsNullOrEmpty(provider.Credential));

	private static ProviderRecording ForApiKey(ApiKeyProvider provider) =>
		provider switch
		{
			ApiKeyProvider.Tmdb => Build(provider, TmdbOptions.SectionName, "_API_KEY"),
			ApiKeyProvider.Mdblist => Build(provider, MdblistOptions.SectionName, "_API_KEY"),
		};

	private static ProviderRecording ForAuth(AuthProvider provider) =>
		provider switch
		{
			AuthProvider.Plex => Build(provider, PlexOptions.SectionName, "_TOKEN"),
		};

	private static ProviderRecording Build<T>(T provider, string section, string keySuffix)
		where T : struct, Enum => new(Slug(provider), section, AppSettings[$"{section}:BaseUrl"]!, $"{Key(provider)}{keySuffix}");

	private static string Slug<T>(T provider)
		where T : struct, Enum => provider.ToString().ToLowerInvariant();

	private static string Key<T>(T provider)
		where T : struct, Enum => provider.ToString().ToUpperInvariant();
}
