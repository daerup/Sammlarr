using System.ComponentModel;

namespace Contracts.Media;

public enum MediaProvider
{
	[Description("The Movie Database")]
	Tmdb,

	[Description("Internet Movie Database")]
	Imdb,

	[Description("The TV Show Database")]
	Tvdb,

	[Description("Plex")]
	Plex,
}
