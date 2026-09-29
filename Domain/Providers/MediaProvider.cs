using System.ComponentModel;

namespace Domain.Providers;

public enum MediaProvider
{
	[Description("TMDB")]
	[Capabilities<ProviderCapability>(
		ProviderCapability.Trending | ProviderCapability.Rating | ProviderCapability.Feed | ProviderCapability.ApiKey | ProviderCapability.Artwork
	)]
	Tmdb,

	[Description("IMDb")]
	Imdb,

	[Description("TVDB")]
	Tvdb,

	[Description("Plex")]
	[Capabilities<ProviderCapability>(ProviderCapability.Search | ProviderCapability.Watchlist | ProviderCapability.Auth | ProviderCapability.Artwork)]
	Plex,

	[Description("Mdblist")]
	[Capabilities<ProviderCapability>(ProviderCapability.Feed | ProviderCapability.ApiKey)]
	Mdblist,
}
