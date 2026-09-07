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
