using System.ComponentModel;

namespace Contracts.Media;

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
}
