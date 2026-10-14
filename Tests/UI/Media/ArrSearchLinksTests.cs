using Domain.Media;
using Domain.Wantlist.Arr;
using TestInfrastructure;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class ArrSearchLinksTests
{
	private static readonly ArrSinkConfig Addressed = new(
		ArrInstanceConfig.Empty with
		{
			Url = Some.Of("http://radarr:7878"),
		},
		ArrInstanceConfig.Empty with
		{
			Url = Some.Of("http://sonarr:8989/"),
		},
		ArrTagScheme.Default
	);

	private static readonly ImdbId MatrixImdb = ImdbId.From("tt0133093", MediaType.Movie);

	private static readonly ImdbId BreakingBadImdb = ImdbId.From("tt0903747", MediaType.Show);

	public static IEnumerable<Func<(IExternalMediaId[] Ids, ArrApp App, string Term, string Url)>> FindableTitles()
	{
		return
		[
			() => ([MatrixImdb, TmdbId.From("603", MediaType.Movie)], ArrApp.Radarr, "tmdb:603", "http://radarr:7878/add/new?term=tmdb%3A603"),
			() => ([MatrixImdb], ArrApp.Radarr, "imdb:tt0133093", "http://radarr:7878/add/new?term=imdb%3Att0133093"),
			() =>
				(
					[BreakingBadImdb, TmdbId.From("1396", MediaType.Show), TvdbId.From("81189", MediaType.Show)],
					ArrApp.Sonarr,
					"tvdb:81189",
					"http://sonarr:8989/add/new?term=tvdb%3A81189"
				),
			() => ([BreakingBadImdb, TmdbId.From("1396", MediaType.Show)], ArrApp.Sonarr, "tmdb:1396", "http://sonarr:8989/add/new?term=tmdb%3A1396"),
			() => ([BreakingBadImdb], ArrApp.Sonarr, "imdb:tt0903747", "http://sonarr:8989/add/new?term=imdb%3Att0903747"),
		];
	}

	[Test]
	[MethodDataSource(nameof(FindableTitles))]
	public async Task For_AMovieOrAShow_LinksToItsAppsSearchByTheBestIdItHasWithoutATrailingSlash(IExternalMediaId[] ids, ArrApp app, string term, string url)
	{
		await Assert.That(ArrSearchLinks.For(Addressed, ids)).IsSome().And.IsEqualTo(new ArrSearchLink(app, term, url));
	}

	[Test]
	public async Task For_ATitleWithOnlyAPlexId_IsNone()
	{
		await Assert.That(ArrSearchLinks.For(Addressed, [PlexId.From("5d776", MediaType.Movie)])).IsNone();
	}

	[Test]
	public async Task For_AnIdOfUnknownType_IsNone()
	{
		await Assert.That(ArrSearchLinks.For(Addressed, [TmdbId.From("603", MediaType.Unknown)])).IsNone();
	}

	[Test]
	public async Task For_AnAppWithoutAnAddress_IsNone()
	{
		await Assert.That(ArrSearchLinks.For(ArrSinkConfig.Empty, [TmdbId.From("603", MediaType.Movie)])).IsNone();
	}

	[Test]
	[Arguments("javascript:alert(1)//")]
	[Arguments("radarr:7878")]
	[Arguments("not an address")]
	public async Task For_AnAddressThatIsNotHttp_IsNone(string address)
	{
		var config = ArrSinkConfig.Empty.With(ArrApp.Radarr, ArrInstanceConfig.Empty with { Url = Some.Of(address) });

		await Assert.That(ArrSearchLinks.For(config, [TmdbId.From("603", MediaType.Movie)])).IsNone();
	}

	[Test]
	public async Task For_AnAddressWithABaseAndAQuery_KeepsTheBaseAndDropsTheQuery()
	{
		var config = ArrSinkConfig.Empty.With(ArrApp.Radarr, ArrInstanceConfig.Empty with { Url = Some.Of("https://home.example/radarr/?tab=1#top") });

		await Assert
			.That(ArrSearchLinks.For(config, [TmdbId.From("603", MediaType.Movie)]).Map(link => link.Url))
			.IsSome()
			.And.IsEqualTo("https://home.example/radarr/add/new?term=tmdb%3A603");
	}

	[Test]
	public async Task For_AnAppWithAnExternalUrl_LinksThereInsteadOfToItsUrl()
	{
		var config = Addressed.With(ArrApp.Radarr, Addressed.Radarr with { ExternalUrl = Some.Of("http://localhost:7879") });

		await Assert
			.That(ArrSearchLinks.For(config, [TmdbId.From("603", MediaType.Movie)]).Map(link => link.Url))
			.IsSome()
			.And.IsEqualTo("http://localhost:7879/add/new?term=tmdb%3A603");
	}
}
