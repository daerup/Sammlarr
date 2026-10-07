using System.Net;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MdblistIdLookupTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BreakingBad = """
		[{"title":"Breaking Bad","year":2008,"type":"show","ids":{"imdb":"tt0903747","trakt":1388,"tmdb":1396,"tvdb":81189,"mal":null,"mdblist":"8plj"},"ratings":[]}]
		""";

	private const string WithoutTvdb = """
		[{"title":"Breaking Bad","year":2008,"type":"show","ids":{"imdb":"tt0903747","trakt":1388,"tmdb":1396,"tvdb":null,"mal":null,"mdblist":"8plj"},"ratings":[]}]
		""";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task FindIdAsync_ForAShowWithOnlyAnImdbIdAndNoTmdbKey_FindsItsTvdbIdThroughMdblist()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/imdb/show", BreakingBad);
		var show = await host.SeedMediaAsync(ImdbId.From("tt0903747", MediaType.Show));

		var found = await host.Resolve<IMediaService>().FindIdAsync(show, MediaProvider.Tvdb);

		await Assert.That(found).IsSome().And.IsEqualTo<IExternalMediaId>(TvdbId.From("81189", MediaType.Show));
	}

	[Test]
	public async Task FindIdAsync_WhileTmdbIsRateLimited_FindsTheTvdbIdThroughMdblistWithoutAskingTmdb()
	{
		await host.WithTmdbAccount("valid-tmdb-key");
		await host.WithMdblistAccount("valid-mdblist-key");
		host.RateLimited(RemoteService.Tmdb);
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/imdb/show", BreakingBad);
		var show = await host.SeedMediaAsync(ImdbId.From("tt0903747", MediaType.Show));

		var found = await host.Resolve<IMediaService>().FindIdAsync(show, MediaProvider.Tvdb);

		await Assert.That(found).IsSome();
		await Assert.That(host.HttpStub.Requests(TestUrls.TmdbApi)).IsEqualTo(0);
	}

	[Test]
	public async Task FindIdAsync_ForAShowNoServiceKnowsATvdbIdFor_AsksEachLookupOnceAndThenNoMore()
	{
		await host.WithTmdbAccount("valid-tmdb-key");
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/imdb/show", WithoutTvdb);
		host.HttpStub.RespondWithJsonForUrl(
			$"{TestUrls.TmdbApi}/find/tt0903747",
			"""{"movie_results":[],"tv_results":[{"id":1396,"original_name":"Breaking Bad","first_air_date":"2008-01-20"}]}"""
		);
		host.HttpStub.RespondWithJsonForUrl(
			$"{TestUrls.TmdbApi}/tv/1396",
			"""{"id":1396,"original_name":"Breaking Bad","first_air_date":"2008-01-20","external_ids":{"imdb_id":"tt0903747","tvdb_id":null}}"""
		);
		host.WikidataHolds("tt0903747");
		host.HttpStub.RespondWithStatusForUrl($"{TestUrls.TvmazeApi}/lookup/shows", HttpStatusCode.NotFound);
		var show = await host.SeedMediaAsync(ImdbId.From("tt0903747", MediaType.Show));
		var media = host.Resolve<IMediaService>();

		var first = await media.FindIdAsync(show, MediaProvider.Tvdb);
		var asked = (
			Mdblist: host.HttpStub.Requests(TestUrls.MdblistApi),
			Tmdb: host.HttpStub.Requests(TestUrls.TmdbApi),
			Wikidata: host.WikidataAsked("tt0903747"),
			Tvmaze: host.HttpStub.Requests(TestUrls.TvmazeApi)
		);
		var again = await media.FindIdAsync(show, MediaProvider.Tvdb);

		await Assert.That(first).IsNone();
		await Assert.That(again).IsNone();
		await Assert.That(asked).IsEqualTo((1, 2, 1, 1));
		await Assert
			.That(
				(
					host.HttpStub.Requests(TestUrls.MdblistApi),
					host.HttpStub.Requests(TestUrls.TmdbApi),
					host.WikidataAsked("tt0903747"),
					host.HttpStub.Requests(TestUrls.TvmazeApi)
				)
			)
			.IsEqualTo(asked);
	}
}
