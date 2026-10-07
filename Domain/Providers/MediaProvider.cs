using System.ComponentModel;

namespace Domain.Providers;

public enum MediaProvider
{
	[Description("TMDB")]
	Tmdb,

	[Description("IMDb")]
	Imdb,

	[Description("Rotten Tomatoes")]
	RottenTomatoes,

	[Description("TVDB")]
	Tvdb,

	[Description("Plex")]
	Plex,

	[Description("MDBList")]
	Mdblist,

	[Description("Metacritic")]
	Metacritic,

	[Description("Letterboxd")]
	Letterboxd,

	[Description("Trakt")]
	Trakt,

	[Description("MyAnimeList")]
	Mal,
}
