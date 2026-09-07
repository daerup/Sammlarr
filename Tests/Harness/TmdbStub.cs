using API.Tmdb;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace Harness;

internal static class TmdbStub
{
	public static void Detail(HttpStub http, ResolvedVersion v, double voteAverage = 0, int voteCount = 0, bool hasVoteData = true)
	{
		var tmdbId = v.Ids.GetValueOrDefault(MediaProvider.Tmdb, "0");
		var endpointType = v.Type == MediaType.Show ? TmdbEndpoints.ShowType : TmdbEndpoints.MovieType;
		http.RespondWithJsonForUrl(string.Format(TmdbEndpoints.Detail, endpointType, tmdbId), DetailJson(tmdbId, v, voteAverage, voteCount, hasVoteData));
	}

	public static string TrendingJson(IReadOnlyList<ResolvedVersion> items, int page) =>
		$$"""
			{
			  "page": {{page}},
			  "results": [{{string.Join(",", items.Select(TrendingItemJson))}}],
			  "total_pages": {{page}},
			  "total_results": {{items.Count}}
			}
			""";

	private static string TrendingItemJson(ResolvedVersion v)
	{
		var tmdbId = v.Ids.GetValueOrDefault(MediaProvider.Tmdb, "0");
		var isShow = v.Type == MediaType.Show;
		var titleField = isShow ? "null" : $"\"{v.Title}\"";
		var nameField = isShow ? $"\"{v.Title}\"" : "null";
		var releaseDateField = isShow ? "null" : "\"2024-01-15\"";
		var firstAirDateField = isShow ? "\"2024-01-15\"" : "null";
		return $$"""
			{
			  "adult": false,
			  "backdrop_path": "/backdrop.jpg",
			  "id": {{tmdbId}},
			  "title": {{titleField}},
			  "name": {{nameField}},
			  "original_title": {{titleField}},
			  "original_name": {{nameField}},
			  "overview": "An overview",
			  "poster_path": "/poster.jpg",
			  "media_type": "{{MediaTypeOf(v.Type)}}",
			  "original_language": "en",
			  "genre_ids": [28],
			  "popularity": 1.5,
			  "release_date": {{releaseDateField}},
			  "first_air_date": {{firstAirDateField}},
			  "video": false,
			  "vote_average": 7.5,
			  "vote_count": 100
			}
			""";
	}

	private static string MediaTypeOf(MediaType type) =>
		type switch
		{
			MediaType.Movie => "movie",
			MediaType.Show => "tv",
			_ => "person",
		};

	private static string DetailJson(string tmdbId, ResolvedVersion v, double voteAverage, int voteCount, bool hasVoteData)
	{
		var titleJson = string.IsNullOrEmpty(v.Title) ? "null" : $"\"{v.Title}\"";
		var overviewJson = v.Summary is null ? "null" : $"\"{v.Summary}\"";
		var genresJson = v.Genres is null ? "[]" : $"[{string.Join(",", v.Genres.Select((name, i) => $$"""{ "id": {{i + 1}}, "name": "{{name}}" }"""))}]";
		var voteFields = hasVoteData ? $", \"vote_average\": {voteAverage}, \"vote_count\": {voteCount}" : string.Empty;
		var mediaType = v.Type == MediaType.Show ? "tv" : "movie";
		var nameJson = v.Type == MediaType.Show ? titleJson : "null";
		var titleField = v.Type == MediaType.Show ? "null" : titleJson;
		return $$"""
			{
			  "id": {{tmdbId}},
			  "title": {{titleField}},
			  "name": {{nameJson}},
			  "media_type": "{{mediaType}}",
			  "overview": {{overviewJson}},
			  "genres": {{genresJson}}{{voteFields}}
			}
			""";
	}
}
