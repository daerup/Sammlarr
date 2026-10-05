using API.Arr;
using API.Arr.Radarr;
using API.Arr.Sonarr;
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
	[Arguments(ArrApp.Radarr)]
	[Arguments(ArrApp.Sonarr)]
	public async Task For_AnApp_IsThatAppsApi(ArrApp app)
	{
		await Assert.That(Testee.For(app).App).IsEqualTo(app);
	}
}
