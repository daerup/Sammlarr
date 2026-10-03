using System.Globalization;
using System.Net;
using System.Text;
using API.Plex;
using Domain.Media;
using Domain.Providers;

namespace Harness;

internal static class PlexStub
{
	public static void Lookup(HttpStub http, ResolvedVersion v, double? imdbRating = null, int imdbVotes = 0)
	{
		var plexId = v.Ids.GetValueOrDefault(MediaProvider.Plex, "0");
		http.RespondWithJsonForUrl($"{PlexEndpoints.MetadataPath}/{plexId}", LookupJson(v, plexId, imdbRating, imdbVotes));
	}

	public static void LookupFails(HttpStub http, ResolvedVersion v)
	{
		var plexId = v.Ids.GetValueOrDefault(MediaProvider.Plex, "0");
		http.RespondWithStatusForUrl($"{PlexEndpoints.MetadataPath}/{plexId}", HttpStatusCode.InternalServerError);
	}

	public static void LookupHasNoMetadata(HttpStub http, ResolvedVersion v)
	{
		var plexId = v.Ids.GetValueOrDefault(MediaProvider.Plex, "0");
		http.RespondWithJsonForUrl($"{PlexEndpoints.MetadataPath}/{plexId}", EmptyLookupJson);
	}

	private const string EmptyLookupJson = """
		{
		  "MediaContainer": {
		    "librarySectionID": "1",
		    "librarySectionTitle": "Movies",
		    "offset": 0,
		    "totalSize": 0,
		    "Metadata": []
		  }
		}
		""";

	public static void Search(HttpStub http, string term, IReadOnlyList<ResolvedVersion> results) =>
		http.RespondWithJsonForUrl($"{PlexEndpoints.SearchQueryParam}={Uri.EscapeDataString(term)}", results.Count == 0 ? EmptySearchJson : SearchJson(results));

	public static void SearchFailsOnce(HttpStub http, string term, IReadOnlyList<ResolvedVersion> results)
	{
		var failed = false;
		http.RespondForUrl(
			$"{PlexEndpoints.SearchQueryParam}={Uri.EscapeDataString(term)}",
			_ =>
			{
				if (!failed)
				{
					failed = true;
					return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
				}

				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SearchJson(results), Encoding.UTF8, "application/json") };
			}
		);
	}

	private const string EmptySearchJson = """
		{
		  "MediaContainer": {
		    "size": 0,
		    "SearchResults": []
		  }
		}
		""";

	private static string SearchJson(IReadOnlyList<ResolvedVersion> results) =>
		$$"""
			{
			  "MediaContainer": {
			    "suggestedTerms": [],
			    "identifier": "com.plexapp.plugins.library",
			    "size": 1,
			    "SearchResults": [
			      {
			        "size": {{results.Count}},
			        "SearchResult": [{{string.Join(",", results.Select(SearchMetadataJson))}}]
			      }
			    ]
			  }
			}
			""";

	private static string SearchMetadataJson(ResolvedVersion v)
	{
		var plexId = v.Ids.GetValueOrDefault(MediaProvider.Plex, "0");
		var typeStr = TypeString(v.Type);
		return $$"""
			{
			  "Metadata": {
			    "ratingKey": "{{plexId}}",
			    "title": "{{v.Title}}",
			    "type": "{{typeStr}}",
			    "year": {{v.Year}},
			    "originallyAvailableAt": "{{v.Year}}-01-15",
			    "Guid": [{{GuidsJson(v)}}],
			    "Genre": [{{GenresJson(v)}}]
			  }
			}
			""";
	}

	private static string LookupJson(ResolvedVersion v, string plexId, double? imdbRating, int imdbVotes) =>
		$$"""
			{
			  "MediaContainer": {
			    "librarySectionID": "1",
			    "librarySectionTitle": "Movies",
			    "offset": 0,
			    "totalSize": 1,
			    "Metadata": [
			      {
			        "ratingKey": "{{plexId}}",
			        "type": "{{TypeString(v.Type)}}",
			        "title": "{{v.Title}}",
			        "summary": {{SummaryJson(v)}},
			        "addedAt": 0,
			        "audienceRating": 7.5,
			        "originallyAvailableAt": "{{v.Year}}-01-15",
			        "thumb": "{{v.Poster ?? "/poster.jpg"}}",
			        "art": "/backdrop.jpg",
			        "year": {{v.Year}},
			        "duration": 9000000,
			        "Guid": [{{GuidsJson(v)}}],
			        "Genre": [{{GenresJson(v)}}]{{ImdbJson(imdbRating, imdbVotes)}}
			      }
			    ]
			  }
			}
			""";

	private static string ImdbJson(double? rating, int votes)
	{
		return rating is { } value
			? $$""", "Rating": [{ "image": "imdb://image.rating", "type": "audience", "value": {{value.ToString(CultureInfo.InvariantCulture)}} }], "imdbRatingCount": {{votes}}"""
			: string.Empty;
	}

	private static string SummaryJson(ResolvedVersion v) => v.Summary is null ? "null" : $"\"{v.Summary}\"";

	private static string GenresJson(ResolvedVersion v) =>
		v.Genres is null ? string.Empty : string.Join(",", v.Genres.Select(genre => $$"""{ "tag": "{{genre}}" }"""));

	private static string GuidsJson(ResolvedVersion v)
	{
		var guids = new List<string>();
		foreach (var (provider, id) in v.Ids)
		{
			var prefix = provider switch
			{
				MediaProvider.Tmdb => "tmdb",
				MediaProvider.Imdb => "imdb",
				MediaProvider.Tvdb => "tvdb",
				_ => null,
			};
			if (prefix is not null)
				guids.Add($$"""{ "id": "{{prefix}}://{{id}}" }""");
		}
		return string.Join(",", guids);
	}

	private static string TypeString(MediaType type) => type == MediaType.Show ? "show" : "movie";
}
