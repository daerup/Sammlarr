using Domain.Media;

namespace API.Mdblist;

internal static class MdblistRatings
{
	internal static readonly IReadOnlyDictionary<string, RatingSource> ByName = new Dictionary<string, RatingSource>
	{
		["imdb"] = RatingSource.Imdb,
		["tmdb"] = RatingSource.Tmdb,
		["tomatoes"] = RatingSource.RottenTomatoesCritics,
		["popcorn"] = RatingSource.RottenTomatoesAudience,
		["metacritic"] = RatingSource.Metacritic,
		["letterboxd"] = RatingSource.Letterboxd,
		["trakt"] = RatingSource.Trakt,
		["myanimelist"] = RatingSource.MyAnimeList,
	};
}
