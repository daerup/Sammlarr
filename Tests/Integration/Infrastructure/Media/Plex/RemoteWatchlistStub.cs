using System.Net;
using System.Text;
using System.Web;
using API.Plex;
using Contracts.Media;
using Integration.Tests.Infrastructure.Http;

namespace Integration.Tests.Infrastructure.Media.Plex;

internal static class RemoteWatchlistStub
{
	public static void Install(HttpStub http, RemoteWatchlistState state)
	{
		http.RespondForUrl(PlexEndpoints.WatchlistPath, _ => Json(WatchlistJson(state)));
		http.RespondForUrl(PlexEndpoints.AddToWatchlistPath, req => Mutate(() => state.PlexIds.Add(RatingKey(req))));
		http.RespondForUrl(PlexEndpoints.RemoveFromWatchlistPath, req => Mutate(() => state.PlexIds.Remove(RatingKey(req))));
	}

	private static HttpResponseMessage Mutate(Action mutation)
	{
		mutation();
		return new HttpResponseMessage(HttpStatusCode.OK);
	}

	private static string RatingKey(HttpRequestMessage req) =>
		HttpUtility.ParseQueryString(req.RequestUri?.Query ?? string.Empty)[PlexEndpoints.RatingKeyParam] ?? string.Empty;

	private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	private static string WatchlistJson(RemoteWatchlistState state)
	{
		var items = state.PlexIds.Where(state.Known.ContainsKey).Select(id => state.Known[id]).ToList();
		return $$"""
			{
			  "MediaContainer": {
			    "identifier": "watchlist",
			    "offset": 0,
			    "totalSize": {{items.Count}},
			    "size": {{items.Count}},
			    "Metadata": [{{string.Join(",", items.Select(MetadataJson))}}]
			  }
			}
			""";
	}

	private static string MetadataJson(ResolvedVersion v)
	{
		var plexId = v.Ids.GetValueOrDefault(MediaProvider.Plex, "0");
		var type = v.Type == MediaType.Show ? "show" : "movie";
		return $$"""
			{
			  "ratingKey": "{{plexId}}",
			  "type": "{{type}}",
			  "title": "{{v.Title}}",
			  "year": {{v.Year}},
			  "originallyAvailableAt": "{{v.Year}}-01-15",
			  "art": "/art.jpg",
			  "rating": 7.5,
			  "addedAt": 0,
			  "isContinuingSeries": false,
			  "thumb": "/thumb.jpg"
			}
			""";
	}
}
