namespace Integration.Tests.Infrastructure.Media;

[Binding]
public class PlexResolutionSteps(SammlarrScenarioContext sammlarr, MediaCatalogContext catalog)
{
	[Given("the Plex provider can resolve {string}")]
	public void GivenPlexCanResolve(string key)
	{
		var media = catalog[key];
		sammlarr.HttpHandler.RespondWithJsonForUrl(TestUrls.PlexDiscover, BuildPlexSearchJson(media));
		sammlarr.HttpHandler.RespondWithJsonForUrl($"library/metadata/plex-{media.TmdbId}", BuildPlexLookupJson(media));
	}

	[Given("the Plex provider cannot resolve {string}")]
	public void GivenPlexCannotResolve(string key)
	{
		sammlarr.HttpHandler.RespondWithJsonForUrl(
			TestUrls.PlexDiscover,
			"""
			{
			  "MediaContainer": {
			    "size": 0,
			    "SearchResults": []
			  }
			}
			"""
		);
	}

	private static string BuildPlexSearchJson(RegisteredMedia media) =>
		$$"""
			{
			  "MediaContainer": {
			    "suggestedTerms": [],
			    "identifier": "com.plexapp.plugins.library",
			    "size": 1,
			    "SearchResults": [
			      {
			        "size": 1,
			        "SearchResult": [
			          {
			            "Metadata": {
			              "ratingKey": "plex-{{media.TmdbId}}",
			              "title": "{{media.Title}}",
			              "type": "movie",
			              "year": 2024,
			              "originallyAvailableAt": "2024-01-15",
			              "Genre": []
			            }
			          }
			        ]
			      }
			    ]
			  }
			}
			""";

	private static string BuildPlexLookupJson(RegisteredMedia media) =>
		$$"""
			{
			  "MediaContainer": {
			    "librarySectionID": "1",
			    "librarySectionTitle": "Movies",
			    "offset": 0,
			    "totalSize": 1,
			    "Metadata": [
			      {
			        "ratingKey": "plex-{{media.TmdbId}}",
			        "type": "movie",
			        "title": "{{media.Title}}",
			        "addedAt": 0,
			        "audienceRating": 7.5,
			        "originallyAvailableAt": "2024-01-15",
			        "thumb": "/poster.jpg",
			        "art": "/backdrop.jpg",
			        "year": 2024,
			        "duration": 9000000,
			        "Guid": [{ "id": "tmdb://{{media.TmdbId}}" }],
			        "Genre": []
			      }
			    ]
			  }
			}
			""";
}
