using Domain.Media;
using Domain.Providers;

namespace Harness;

internal static class MalFeedStub
{
	internal static void Season(HttpStub http, string identifier, IReadOnlyList<FeedItemSpec> items)
	{
		http.RespondWithJsonForUrl($"{TestUrls.Mal}/{identifier}", $$$"""{"data":[{{{string.Join(",", items.Select(NodeJson))}}}],"paging":{}}""");
	}

	private static string NodeJson(FeedItemSpec item)
	{
		var mediaType = item.Type == MediaType.Movie ? "movie" : "tv";
		var year = item.Year ?? SammlarrTestHost.DefaultYear;
		return $$$"""{"node":{"id":{{{item.Ids[MediaProvider.Mal]}}},"title":"{{{item.Title}}}","media_type":"{{{mediaType}}}","start_date":"{{{year}}}-01-15"}}""";
	}
}
