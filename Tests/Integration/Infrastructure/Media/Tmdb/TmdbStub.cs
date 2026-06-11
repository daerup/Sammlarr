using API.Tmdb;
using Contracts.Media;
using Integration.Tests.Infrastructure.Http;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Infrastructure.Media.Tmdb;

internal static class TmdbStub
{
	public static void Detail(HttpStub http, ResolvedVersion v, double voteAverage = 0, int voteCount = 0, bool hasVoteData = true)
	{
		var tmdbId = v.Ids.GetValueOrDefault(MediaProvider.Tmdb, "0");
		http.RespondWithJsonForUrl(
			string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, tmdbId),
			DetailJson(tmdbId, v, voteAverage, voteCount, hasVoteData)
		);
	}

	public static string TrendingJson(IReadOnlyList<ResolvedVersion> items, int page)
	{
		var results = string.Join(",", items.Select(TrendingItemJson));
		return $$"""
			{
			  "page": {{page}},
			  "results": [{{results}}],
			  "total_pages": {{page}},
			  "total_results": {{items.Count}}
			}
			""";
	}

	private static string DetailJson(string tmdbId, ResolvedVersion v, double voteAverage, int voteCount, bool hasVoteData)
	{
		var titleJson = string.IsNullOrEmpty(v.Title) ? "null" : $"\"{v.Title}\"";
		var overviewJson = v.Summary is null ? "null" : $"\"{v.Summary}\"";
		var genresJson = v.Genres is null ? "[]" : $"[{string.Join(",", v.Genres.Select((name, i) => $$"""{ "id": {{i + 1}}, "name": "{{name}}" }"""))}]";
		var voteFields = hasVoteData ? $", \"vote_average\": {voteAverage}, \"vote_count\": {voteCount}" : string.Empty;
		return $$"""
			{
			  "id": {{tmdbId}},
			  "title": {{titleJson}},
			  "name": null,
			  "media_type": "movie",
			  "overview": {{overviewJson}},
			  "genres": {{genresJson}}{{voteFields}}
			}
			""";
	}

	private static string TrendingItemJson(ResolvedVersion v)
	{
		var tmdbId = v.Ids.GetValueOrDefault(MediaProvider.Tmdb, "0");
		var mediaTypeStr = MediaTypeOf(v.Type);
		return $$"""
			{
			  "adult": false,
			  "backdrop_path": "/backdrop.jpg",
			  "id": {{tmdbId}},
			  "title": "{{v.Title}}",
			  "original_title": "{{v.Title}}",
			  "overview": "An overview",
			  "poster_path": "/poster.jpg",
			  "media_type": "{{mediaTypeStr}}",
			  "original_language": "en",
			  "genre_ids": [28],
			  "popularity": 1.5,
			  "release_date": "2024-01-15",
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
}
