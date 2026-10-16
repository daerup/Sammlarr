namespace E2E.Tests.Recording;

public class MetadataRecorderTests
{
	private const string Search = "http://localhost:4100/tmdb/search/multi?query=Project+Hail+Mary&include_adult=false&api_key=typed";

	[Test]
	[Arguments("https://api.radarr.video/v1/movie/687163", "radarr-metadata", "/v1/movie/687163")]
	[Arguments("https://skyhook.sonarr.tv/v1/tvdb/shows/en/81189", "sonarr-metadata", "/v1/tvdb/shows/en/81189")]
	[Arguments(Search, "tmdb", "/search/multi?query=Project+Hail+Mary&include_adult=false&api_key=typed")]
	public async Task Find_ForAHostOrProviderWithFixtures_NamesItsFolderAndPath(string url, string folder, string pathAndQuery)
	{
		var recordable = MetadataRecorder.Find("GET", url);

		await Assert.That(recordable?.Endpoint.Name).IsEqualTo(folder);
		await Assert.That(recordable?.PathAndQuery).IsEqualTo(pathAndQuery);
	}

	[Test]
	[Arguments("GET", "https://radarr.servarr.com/v1/time")]
	[Arguments("GET", "https://image.tmdb.org/t/p/original/poster.jpg")]
	[Arguments("GET", "http://localhost:4100/plex-blocked/BaseUrl/api/v2/user")]
	[Arguments("GET", "http://localhost:4100/plex-discover/library/search")]
	[Arguments("GET", "http://localhost:4100/radarr/api/v3/system/status")]
	[Arguments("GET", "http://localhost:4100/newznab/api?t=caps")]
	[Arguments("POST", "https://api.radarr.video/v1/movie/bulk")]
	public async Task Find_ForAStubAPlexCallAnAppOrAWrite_IsNothingToRecord(string method, string url)
	{
		await Assert.That(MetadataRecorder.Find(method, url)).IsNull();
	}

	[Test]
	public async Task RealUrl_ForAProvider_SwapsOnlyTheKeyOnTheRealHost()
	{
		var recordable = MetadataRecorder.Find("GET", Search)!;

		await Assert.That(recordable.NeedsSecret).IsTrue();
		await Assert
			.That(recordable.RealUrl("k/y"))
			.IsEqualTo("https://api.themoviedb.org/3/search/multi?query=Project+Hail+Mary&include_adult=false&api_key=k%2Fy");
	}

	[Test]
	public async Task RealUrl_ForAMetadataHost_IsTheUrlRadarrAskedFor()
	{
		var recordable = MetadataRecorder.Find("GET", "https://api.radarr.video/v1/movie/687163")!;

		await Assert.That(recordable.NeedsSecret).IsFalse();
		await Assert.That(recordable.RealUrl(string.Empty)).IsEqualTo("https://api.radarr.video/v1/movie/687163");
	}
}
