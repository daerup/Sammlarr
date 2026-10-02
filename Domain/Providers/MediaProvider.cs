using System.ComponentModel;

namespace Domain.Providers;

public enum MediaProvider
{
	[Description("TMDB")]
	[Capabilities<ProviderCapability>(
		ProviderCapability.Trending | ProviderCapability.Feed | ProviderCapability.ApiKey | ProviderCapability.Artwork | ProviderCapability.Enricher
	)]
	Tmdb,

	[Description("IMDb")]
	Imdb,

	[Description("TVDB")]
	Tvdb,

	[Description("Plex")]
	[Capabilities<ProviderCapability>(ProviderCapability.Search | ProviderCapability.Auth | ProviderCapability.Artwork | ProviderCapability.Enricher)]
	Plex,

	[Description("Mdblist")]
	[Capabilities<ProviderCapability>(ProviderCapability.Feed | ProviderCapability.ApiKey)]
	Mdblist,
}
