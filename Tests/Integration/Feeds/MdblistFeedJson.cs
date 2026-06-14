using Contracts.Media;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Feeds;

internal static class MdblistFeedJson
{
	public static string Build(IReadOnlyList<FeedItemSpec> items)
	{
		var indexed = items.Select((spec, i) => (Spec: spec, Index: i + 1)).ToList();
		var movieRows = string.Join(",", indexed.Where(x => x.Spec.Type != MediaType.Show).Select(x => ItemJson(x.Spec, x.Index)));
		var showRows = string.Join(",", indexed.Where(x => x.Spec.Type == MediaType.Show).Select(x => ItemJson(x.Spec, x.Index)));
		return $$"""
			{
			  "movies": [{{movieRows}}],
			  "shows": [{{showRows}}]
			}
			""";
	}

	private static string ItemJson(FeedItemSpec item, int index)
	{
		var titleJson = item.Title is null ? "null" : $"\"{item.Title}\"";
		var resolvedYear = item.Year ?? MediaIdDefaults.DefaultYear;
		var releaseDateJson = $"\"{resolvedYear}-01-15\"";
		var mediatype = item.Type == MediaType.Show ? "show" : "movie";

		var imdb = item.Ids.TryGetValue(MediaProvider.Imdb, out var imdbId) ? $"\"{imdbId}\"" : "null";
		var tmdb = item.Ids.TryGetValue(MediaProvider.Tmdb, out var tmdbId) ? tmdbId : "null";
		var tvdb = item.Ids.TryGetValue(MediaProvider.Tvdb, out var tvdbId) ? $"\"{tvdbId}\"" : "null";
		var mdblist = item.Ids.TryGetValue(MediaProvider.Mdblist, out var mdblistId) ? $"\"{mdblistId}\"" : "null";

		return $$"""
			{
			  "id": {{index}},
			  "ids": {"imdb": {{imdb}}, "tmdb": {{tmdb}}, "tvdb": {{tvdb}}, "mdblist": {{mdblist}}, "mal": null},
			  "rank": {{index}},
			  "title": {{titleJson}},
			  "status": "Released",
			  "country": "US",
			  "runtime": 139,
			  "mediatype": "{{mediatype}}",
			  "release_date": {{releaseDateJson}},
			  "release_year": {{resolvedYear}},
			  "spoken_language": "English"
			}
			""";
	}
}
