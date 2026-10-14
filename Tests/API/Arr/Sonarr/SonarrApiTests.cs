using System.Net;
using System.Text.Json.Nodes;
using API.Arr;
using API.Arr.Sonarr;
using API.Arr.Types;
using Domain.Media;
using Domain.Wantlist.Arr;
using Shared.Options;
using TUnit.Assertions.Enums;
using TUnit.Mocks.Http;

namespace API.Tests.Arr.Sonarr;

public class SonarrApiTests
{
	private static readonly ArrConnection Connection = new(ArrApp.Sonarr, new Uri("http://sonarr:8989/"), "key");

	private static readonly ArrAddition Addition = new(4, "/tv", [1]);

	private readonly MockHttpHandler _sonarr = Mock.HttpHandler();

	private SonarrApi Testee => new(new HttpClient(_sonarr), new SonarrOptions());

	[Test]
	public async Task DeleteManyAsync_SendsTheSeriesIdsWithoutAnImportListExclusion()
	{
		_sonarr.OnAnyRequest().Respond(HttpStatusCode.OK);

		await Testee.DeleteManyAsync(Connection, [20, 21], ArrInstanceConfig.Empty, CancellationToken.None);

		_sonarr.Verify(
			request =>
				request.Method(HttpMethod.Delete).Path("/api/v3/series/editor").BodyContains("\"seriesIds\":[20,21]").BodyContains("\"addImportListExclusion\":false"),
			Times.Once
		);
	}

	[Test]
	public async Task RetagAsync_SendsTheSeriesIdsToTheEditor()
	{
		_sonarr.OnAnyRequest().Respond(HttpStatusCode.OK);

		await Testee.RetagAsync(Connection, [20], [1], ArrTagChange.Remove, CancellationToken.None);

		_sonarr.Verify(
			request => request.Method(HttpMethod.Put).Path("/api/v3/series/editor").BodyContains("\"seriesIds\":[20]").BodyContains("\"applyTags\":\"remove\""),
			Times.Once
		);
	}

	[Test]
	public async Task IdsOf_AShowWithATmdbId_IsItsTvdbIdThenItsTmdbId()
	{
		var show = new ArrTitle(20, "Breaking Bad", 2008, 1396, 81189, "tt0903747", []);
		IExternalMediaId[] ids = [TvdbId.From("81189", MediaType.Show), TmdbId.From("1396", MediaType.Show)];

		await Assert.That(Testee.IdsOf(show)).IsEquivalentTo(ids, CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdsOf_AShowWithoutATmdbId_IsItsTvdbIdOnly()
	{
		var show = new ArrTitle(20, "Breaking Bad", 2008, 0, 81189, null, []);
		IExternalMediaId[] tvdb = [TvdbId.From("81189", MediaType.Show)];

		await Assert.That(Testee.IdsOf(show)).IsEquivalentTo(tvdb);
	}

	[Test]
	public Task Prepare_WithTheDefaultOptions_AddsAStandardSeriesInSeasonFolders()
	{
		return VerifyJson(Testee.Prepare(Lookup(), ArrInstanceConfig.Empty, Addition).ToJsonString());
	}

	[Test]
	public Task Prepare_ForAnimeWithoutSeasonFolders_SendsTheAnimeTypeAndNoSeasonFolders()
	{
		var sonarr = new SonarrApi(new HttpClient(_sonarr), new SonarrOptions { SeriesType = SonarrSeriesType.Anime, SeasonFolder = false });

		return VerifyJson(sonarr.Prepare(Lookup(), ArrInstanceConfig.Empty with { Monitored = false, SearchOnAdd = false }, Addition).ToJsonString());
	}

	[Test]
	public async Task Prepare_ForTheAnimeRoute_SendsTheAnimeSeriesTypeWhateverTheOption()
	{
		var sonarr = new SonarrApi(new HttpClient(_sonarr), new SonarrOptions { SeriesType = SonarrSeriesType.Daily });

		var prepared = sonarr.Prepare(Lookup(), ArrInstanceConfig.Empty, Addition with { Route = ArrRoute.Anime });

		await Assert.That(prepared["seriesType"]!.GetValue<string>()).IsEqualTo("anime");
	}

	private static JsonObject Lookup() => new() { ["title"] = "Breaking Bad", ["tvdbId"] = 81189 };
}
