using System.Net;
using API.Arr;
using API.Arr.Radarr;
using API.Arr.Sonarr;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TUnit.Mocks.Http;

namespace API.Tests.Arr;

public class ArrSearchTests
{
	private static readonly ArrSinkConfig Connected = new(
		new ArrInstanceConfig { Url = Some.Of("http://radarr:7878"), ApiKey = Some.Of("key") },
		new ArrInstanceConfig { Url = Some.Of("http://sonarr:8989"), ApiKey = Some.Of("key") },
		ArrTagScheme.Default
	);

	private readonly MockHttpHandler _radarr = Mock.HttpHandler();

	private readonly MockHttpHandler _sonarr = Mock.HttpHandler();

	[Test]
	public async Task SearchAsync_ForMoviesAndShows_AsksRadarrForMoviesAndSonarrForShows()
	{
		_radarr
			.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/movie/lookup"))
			.RespondWithJson(
				"""[{"id":0,"title":"Project Hail Mary","year":2026,"tmdbId":687163,"imdbId":"tt12042730","overview":"A teacher wakes up alone.","remotePoster":"https://image.tmdb.org/t/p/original/p.jpg","tags":[]}]"""
			);
		_sonarr
			.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/series/lookup"))
			.RespondWithJson("""[{"id":0,"title":"Hail Mary","year":2023,"tvdbId":424242,"tmdbId":95557,"imdbId":"","tags":[]}]""");

		var found = (await Testee().SearchAsync("project hail", [MediaType.Movie, MediaType.Show])).Found;

		string?[] titles = ["Project Hail Mary", "Hail Mary"];
		await Assert.That(found.Select(item => item.Details.Title)).IsEquivalentTo(titles);
		IExternalMediaId[] movie = [TmdbId.From("687163", MediaType.Movie), ImdbId.From("tt12042730", MediaType.Movie)];
		IExternalMediaId[] show = [TvdbId.From("424242", MediaType.Show), TmdbId.From("95557", MediaType.Show)];
		await Assert.That(found[0].Externals).IsEquivalentTo(movie);
		await Assert.That(found[1].Externals).IsEquivalentTo(show);
		await Assert.That(found[0].Details.PosterUrl).IsEqualTo("https://image.tmdb.org/t/p/original/p.jpg");
		_radarr.Verify(request => request.PathMatches(".*/movie/lookup\\?term=project%20hail$"), Times.Once);
	}

	[Test]
	public async Task SearchAsync_ForShowsOnly_AsksOnlySonarr()
	{
		_sonarr.OnAnyRequest().RespondWithJson("[]");

		await Testee().SearchAsync("hail", [MediaType.Show]);

		_radarr.Verify(request => request.Method(HttpMethod.Get), Times.Never);
	}

	[Test]
	public async Task SearchAsync_WhenRadarrRefusesTheKey_SaysWhereToFixIt()
	{
		_radarr.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);

		await Assert
			.That(async () => await Testee().SearchAsync("hail", [MediaType.Movie]))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("Check the API key under Settings › General in Radarr.", StringComparison.Ordinal);
	}

	[Test]
	public async Task SearchAsync_WhenSonarrIsDown_ReturnsRadarrsMoviesAndNamesSonarr()
	{
		_radarr.OnAnyRequest().RespondWithJson("""[{"id":0,"title":"Project Hail Mary","year":2026,"tmdbId":687163,"tags":[]}]""");
		_sonarr.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);

		var answer = await Testee().SearchAsync("project hail", [MediaType.Movie, MediaType.Show]);

		string?[] titles = ["Project Hail Mary"];
		await Assert.That(answer.Found.Select(item => item.Details.Title)).IsEquivalentTo(titles);
		await Assert.That(answer.Missed.Select(miss => miss.Service)).IsEquivalentTo([RemoteService.Sonarr]);
		await Assert.That(answer.Missed.Single().Reason).Contains("Sonarr answered 503");
	}

	[Test]
	public async Task SearchAsync_WhenSonarrIsNotSetUp_NamesNoMiss()
	{
		_radarr.OnAnyRequest().RespondWithJson("""[{"id":0,"title":"Project Hail Mary","year":2026,"tmdbId":687163,"tags":[]}]""");
		var radarrOnly = Connected with { Sonarr = ArrInstanceConfig.Empty };

		var answer = await Testee(radarrOnly).SearchAsync("project hail", [MediaType.Movie, MediaType.Show]);

		await Assert.That(answer.Found).Count().IsEqualTo(1);
		await Assert.That(answer.Missed).IsEmpty();
	}

	[Test]
	public async Task SearchAsync_WhenBothAppsFail_Throws()
	{
		_radarr.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);
		_sonarr.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);

		await Assert.That(async () => await Testee().SearchAsync("project hail", [MediaType.Movie, MediaType.Show])).Throws<InvalidOperationException>();
	}

	private ArrSearch Testee() => Testee(Connected);

	private ArrSearch Testee(ArrSinkConfig config)
	{
		var configs = IArrSinkConfigService.Mock();
		configs.GetAsync(Any()).Returns(config);
		var apis = new ArrApis(new RadarrApi(new HttpClient(_radarr), new RadarrOptions()), new SonarrApi(new HttpClient(_sonarr), new SonarrOptions()));
		return new ArrSearch(apis, configs.Object, NullLogger<ArrSearch>.Instance);
	}
}
