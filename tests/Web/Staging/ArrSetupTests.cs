using TUnit.Assertions.Enums;
using TUnit.Mocks.Http;
using Web.Staging;

namespace Web.Tests.Staging;

public class ArrSetupTests
{
	private static readonly ArrAppOptions Sonarr = new()
	{
		Url = "http://sonarr:8989",
		Address = "http://sonarr:8989",
		RootFolder = "/tv",
		AnimeRootFolder = "/anime",
		Library = "tv",
	};

	private readonly MockHttpHandler _sonarr = Mock.HttpHandler();

	[Test]
	public async Task PrepareAsync_ForAnAppWithOnlyItsDefaultFolder_AddsTheAnimeFolderAlone()
	{
		Holds("""[{"path":"/tv"}]""");

		await ArrSetup.PrepareAsync(Testee(), StagedApp.Sonarr, Sonarr, "http://wiremock:8080/newznab", CancellationToken.None);

		_sonarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/rootfolder").BodyContains("\"path\":\"/anime\""), Times.Once);
		_sonarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/rootfolder").BodyContains("\"path\":\"/tv\""), Times.Never);
	}

	[Test]
	public async Task PrepareAsync_ForAnEmptyApp_AddsTheDefaultFolderThenTheAnimeFolder()
	{
		Holds("[]");

		await ArrSetup.PrepareAsync(Testee(), StagedApp.Sonarr, Sonarr, "http://wiremock:8080/newznab", CancellationToken.None);

		var posted = _sonarr.Requests.Where(request => request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/v3/rootfolder");
		await Assert.That(posted.Select(request => request.Body!)).IsEquivalentTo(["""{"path":"/tv"}""", """{"path":"/anime"}"""], CollectionOrdering.Matching);
	}

	private void Holds(string folders)
	{
		_sonarr.OnGet("/ping").Respond();
		_sonarr.OnGet("/api/v3/rootfolder").RespondWithJson(folders);
		_sonarr.OnPost("/api/v3/rootfolder").RespondWithJson("{}");
		_sonarr.OnGet("/api/v3/downloadclient").RespondWithJson("""[{"id":1}]""");
		_sonarr.OnGet("/api/v3/indexer").RespondWithJson("""[{"id":1}]""");
		_sonarr.OnGet("/api/v3/qualityprofile").RespondWithJson("""[{"id":4,"name":"HD-1080p"}]""");
	}

	private ArrClient Testee() => new(new HttpClient(_sonarr) { BaseAddress = new Uri("http://sonarr:8989/") });
}
