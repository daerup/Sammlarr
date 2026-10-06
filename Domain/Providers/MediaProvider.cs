using System.ComponentModel;

namespace Domain.Providers;

// The members' order breaks ties when EnricherChoice.Next picks which enricher to ask.
public enum MediaProvider
{
	[Description("TMDB")]
	[Capabilities<ProviderCapability>(
		ProviderCapability.Trending | ProviderCapability.Feed | ProviderCapability.ApiKey | ProviderCapability.Artwork | ProviderCapability.Enricher
	)]
	Tmdb,

	[Description("IMDb")]
	Imdb,

	[Description("Rotten Tomatoes")]
	RottenTomatoes,

	[Description("TVDB")]
	[Capabilities<ProviderCapability>(ProviderCapability.Artwork)]
	Tvdb,

	[Description("Plex")]
	[Capabilities<ProviderCapability>(ProviderCapability.Artwork | ProviderCapability.Enricher)]
	Plex,

	[Description("MDBList")]
	[Capabilities<ProviderCapability>(ProviderCapability.Feed | ProviderCapability.ApiKey)]
	Mdblist,
}
