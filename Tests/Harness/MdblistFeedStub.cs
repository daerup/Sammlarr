using System.Net;
using System.Text;
using System.Web;
using API.Mdblist;
using Domain.Media;
using Domain.Providers;
using Shared.Concurrency;

namespace Harness;

internal sealed record FeedItemSpec(string? Title, MediaType Type, int? Year, IReadOnlyDictionary<MediaProvider, string> Ids);

internal sealed record FeedPage(int Offset, bool HasMore, IReadOnlyList<string> Titles);

internal static class MdblistFeedStub
{
	internal static void Contains(HttpStub http, string identifier, IReadOnlyList<FeedItemSpec> items) =>
		http.RespondWithJsonForUrl(Url(identifier), Build(items));

	internal static void ReturnsStatus(HttpStub http, string identifier, HttpStatusCode status) => http.RespondWithStatusForUrl(Url(identifier), status);

	internal static void ReturnsInvalidJson(HttpStub http, string identifier) =>
		http.RespondForUrl(
			Url(identifier),
			_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>not json</html>", Encoding.UTF8, "application/json") }
		);

	internal static HeldRequest Hold(HttpStub http, string identifier) => http.Hold(Url(identifier));

	internal static void Empty(HttpStub http, string identifier) => http.RespondWithJsonForUrl(Url(identifier), """{ "movies": [], "shows": [] }""");

	internal static void Unavailable(HttpStub http, string identifier) =>
		http.RespondForUrl(Url(identifier), _ => throw new LaneRefusedException("MDBList is down."));

	internal static void PartialThenUnavailable(HttpStub http, string identifier, IReadOnlyList<FeedItemSpec> items) =>
		http.RespondForUrl(
			Url(identifier),
			request =>
			{
				if (Offset(request) > 0)
				{
					throw new LaneRefusedException("MDBList is down.");
				}

				var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Build(items), Encoding.UTF8, "application/json") };
				response.Headers.TryAddWithoutValidation("X-Has-More", "true");
				return response;
			}
		);

	internal static void Paginated(HttpStub http, string identifier, IReadOnlyList<FeedPage> pages)
	{
		var byOffset = pages.ToDictionary(page => page.Offset);
		http.RespondForUrl(
			Url(identifier),
			request =>
			{
				var page = byOffset[Offset(request)];
				var items = page.Titles.Select(title => new FeedItemSpec(title, MediaType.Movie, null, new Dictionary<MediaProvider, string>())).ToList();
				var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Build(items), Encoding.UTF8, "application/json") };
				if (page.HasMore)
				{
					response.Headers.TryAddWithoutValidation("X-Has-More", "true");
				}

				return response;
			}
		);
	}

	private static string Url(string identifier) => MdblistLists.ItemsPath(identifier);

	private static int Offset(HttpRequestMessage request) => int.Parse(HttpUtility.ParseQueryString(request.RequestUri?.Query ?? string.Empty)["offset"] ?? "0");

	private static string Build(IReadOnlyList<FeedItemSpec> items)
	{
		var indexed = items.Select((spec, i) => (Spec: spec, Index: i + 1)).ToList();
		var movieRows = string.Join(",", indexed.Where(entry => entry.Spec.Type != MediaType.Show).Select(entry => ItemJson(entry.Spec, entry.Index)));
		var showRows = string.Join(",", indexed.Where(entry => entry.Spec.Type == MediaType.Show).Select(entry => ItemJson(entry.Spec, entry.Index)));
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
		var resolvedYear = item.Year ?? SammlarrTestHost.DefaultYear;
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
