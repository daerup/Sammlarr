using System.ComponentModel;

namespace Domain.Providers;

public enum MediaProvider
{
	[Description("TMDB")]
	Tmdb,

	[Description("IMDb")]
	Imdb,

	[Description("TVDB")]
	Tvdb,

	[Description("Plex")]
	Plex,

	[Description("Mdblist")]
	Mdblist,
}
