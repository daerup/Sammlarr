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

	public static void Match(HttpStub http, ResolvedVersion v)
	{
		var plexId = v.Ids.GetValueOrDefault(MediaProvider.Plex, "0");
		foreach (var guid in Guids(v))
		{
			http.RespondWithJsonForUrl(MatchUrl(v.Type, guid), MatchJson(plexId, v));
		}
	}

	public static void MatchNothing(HttpStub http, ResolvedVersion v)
	{
		foreach (var guid in Guids(v))
		{
			http.RespondWithJsonForUrl(MatchUrl(v.Type, guid), EmptyMatchJson);
		}
	}

	public static void MatchFails(HttpStub http, ResolvedVersion v, HttpStatusCode status)
	{
		foreach (var guid in Guids(v))
		{
			http.RespondWithStatusForUrl(MatchUrl(v.Type, guid), status);
		}
	}

	public static void MatchFailsOnce(HttpStub http, ResolvedVersion v)
	{
		var plexId = v.Ids.GetValueOrDefault(MediaProvider.Plex, "0");
		var failed = false;
		foreach (var guid in Guids(v))
		{
			http.RespondForUrl(
				MatchUrl(v.Type, guid),
				_ =>
				{
					if (!failed)
					{
						failed = true;
						return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
					}

					return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MatchJson(plexId, v), Encoding.UTF8, "application/json") };
				}
			);
		}
	}

	private const string EmptyMatchJson = """{ "MediaContainer": { "size": 0 } }""";

	private static string MatchUrl(MediaType type, string guid)
	{
		return $"{PlexEndpoints.MatchesPath}?type={(type == MediaType.Show ? 2 : 1)}&guid={Uri.EscapeDataString(guid)}";
	}

	private static string MatchJson(string plexId, ResolvedVersion v)
	{
		return $$"""{ "MediaContainer": { "size": 1, "Metadata": [ { "ratingKey": "{{plexId}}", "type": "{{TypeString(v.Type)}}", "title": "{{v.Title}}", "year": {{v.Year}} } ] } }""";
	}

	public static void Search(HttpStub http, string term, IReadOnlyList<ResolvedVersion> results) =>
		http.RespondWithJsonForUrl($"{PlexEndpoints.SearchQueryParam}={Uri.EscapeDataString(term)}", results.Count == 0 ? EmptySearchJson : SearchJson(results));

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

	private static string GuidsJson(ResolvedVersion v) => string.Join(",", Guids(v).Select(guid => $$"""{ "id": "{{guid}}" }"""));

	private static IEnumerable<string> Guids(ResolvedVersion v)
	{
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
			{
				yield return $"{prefix}://{id}";
			}
		}
	}

	private static string TypeString(MediaType type) => type == MediaType.Show ? "show" : "movie";
}
