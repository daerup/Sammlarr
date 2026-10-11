using System.Net;
using API.Arr;
using API.Arr.Radarr;
using API.Arr.Sonarr;
using API.Http.Lanes;
using Domain.Media;
using Domain.Wantlist.Arr;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Arr;

public class ArrApisTests
{
	private static readonly ArrApis Testee = new(new RadarrApi(new HttpClient(), new RadarrOptions()), new SonarrApi(new HttpClient(), new SonarrOptions()));

	[Test]
	public async Task Taking_ATmdbMovieId_IsRadarr()
	{
		await Assert.That(Testee.Taking(TmdbId.From("603", MediaType.Movie)).Map(api => api.App)).IsSome().And.IsEqualTo(ArrApp.Radarr);
	}

	[Test]
	public async Task Taking_ATvdbShowId_IsSonarr()
	{
		await Assert.That(Testee.Taking(TvdbId.From("81189", MediaType.Show)).Map(api => api.App)).IsSome().And.IsEqualTo(ArrApp.Sonarr);
	}

	[Test]
	public async Task Taking_ATmdbShowId_IsNobody()
	{
		await Assert.That(Testee.Taking(TmdbId.From("1396", MediaType.Show))).IsNone();
	}

	[Test]
	public async Task Taking_AnImdbId_IsNobody()
	{
		await Assert.That(Testee.Taking(ImdbId.From("tt0133093", MediaType.Movie))).IsNone();
	}

	[Test]
	public async Task StatusAsync_ForAConnection_SendsInTheLaneOfItsAddress()
	{
		var radarr = Mock.HttpHandler();
		var owners = new List<string>();
		radarr
			.OnAnyRequest()
			.RespondWith(request =>
			{
				request.Options.TryGetValue(LaneHandler.Owner, out var owner);
				owners.Add(owner ?? "");
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"appName":"Radarr","version":"6.4.4"}""") };
			});
		var api = new RadarrApi(new HttpClient(radarr), new RadarrOptions());

		await api.StatusAsync(new ArrConnection(ArrApp.Radarr, new Uri("http://radarr:7878/"), "0123456789abcdef0123456789abcdef"), CancellationToken.None);

		await Assert.That(owners).IsEquivalentTo(["http://radarr:7878"]);
	}

	[Test]
	[Arguments(ArrApp.Radarr)]
	[Arguments(ArrApp.Sonarr)]
	public async Task For_AnApp_IsThatAppsApi(ArrApp app)
	{
		await Assert.That(Testee.For(app).App).IsEqualTo(app);
	}
}
