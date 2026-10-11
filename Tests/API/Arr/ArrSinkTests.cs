using System.Net;
using System.Text.Json.Nodes;
using API.Arr;
using API.Arr.Radarr;
using API.Arr.Sonarr;
using Contracts.Datasets;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Arr;

public class ArrSinkTests
{
	private const string Key = "0123456789abcdef0123456789abcdef";

	private static readonly MediaId Known = MediaId.New();

	private static readonly ArrSinkConfig Connected = new(
		new ArrInstanceConfig
		{
			Url = Some.Of("http://radarr:7878"),
			ApiKey = Some.Of(Key),
			QualityProfileId = Some.Of(1),
			RootFolderPath = Some.Of("/movies"),
		},
		new ArrInstanceConfig
		{
			Url = Some.Of("http://sonarr:8989"),
			ApiKey = Some.Of(Key),
			QualityProfileId = Some.Of(4),
			RootFolderPath = Some.Of("/tv"),
		},
		ArrTagScheme.Default
	);

	private static readonly ArrSinkConfig RoutingAnime = Connected with
	{
		Radarr = Connected.Radarr with { AnimeQualityProfileId = Some.Of(8), AnimeRootFolderPath = Some.Of("/anime-movies") },
		Sonarr = Connected.Sonarr with { AnimeRootFolderPath = Some.Of("/anime") },
	};

	private readonly MockHttpHandler _radarr = Mock.HttpHandler();

	private readonly MockHttpHandler _sonarr = Mock.HttpHandler();

	private readonly Mock<IMediaService> _media = IMediaService.Mock();

	private readonly Mock<IAnimeIds> _animeIds = IAnimeIds.Mock();

	private readonly Mock<IUserRepository> _users = IUserRepository.Mock();

	private readonly Mock<ICurrentUser> _viewer = ICurrentUser.Mock();

	private static readonly UserId Guest = UserId.From(Guid.Parse("8f14e45f-ceea-467a-9575-6c0f6e3b4c11"));

	private static readonly User GuestUser = new(Guest, "Guest", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch);

	[Before(Test)]
	public void Everyone()
	{
		_users.GetAllAsync(Any()).Returns([GuestUser]);
		_users.GetAsync(Guest, Any()).Returns(GuestUser);
		_viewer.Id.Getter.Returns(Guest);
		_media.GetStoredDetailsAsync(Any<IEnumerable<MediaId>>(), Any()).Returns(new Dictionary<MediaId, MediaDetails>());
		_animeIds.FindManyAsync(Any(), Any()).Returns(new Dictionary<IExternalMediaId, AnimeIdMapping>());
	}

	[Test]
	[Arguments(MediaType.Movie, MediaProvider.Tmdb)]
	[Arguments(MediaType.Show, MediaProvider.Tvdb)]
	public async Task AddressedBy_ForAMovieOrAShow_IsTheIdTheAppAddsBy(MediaType type, MediaProvider expected)
	{
		await Assert.That(Testee().AddressedBy(type)).IsSome().And.IsEqualTo(expected);
	}

	[Test]
	public async Task AddressedBy_ForAnUnknownType_IsNone()
	{
		await Assert.That(Testee().AddressedBy(MediaType.Unknown)).IsNone();
	}

	[Test]
	public async Task GetAsync_ReadsOnlyTheTitlesCarryingTheUsersTag()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"},{"id":2,"label":"4k"}]""");
		_radarr
			.OnGet("/api/v3/movie")
			.RespondWithJson(
				"""[{"id":10,"title":"Inception","year":2010,"tmdbId":27205,"imdbId":"tt1375666","tags":[1]},{"id":11,"tmdbId":550,"tags":[2]},{"id":12,"tmdbId":603,"tags":[]}]"""
			);
		EmptySonarr();
		Identifying();

		var snapshot = await Testee().GetAsync();

		await Assert.That(snapshot.Ids).IsEquivalentTo([Known]);
		_media
			.IdentifyAsync(Is<IEnumerable<IdentifyRequest>>(requests => requests!.Single().Candidates.Any(id => id.Value == "27205")), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_ForAViewerRenamedSinceTheyJoined_ReadsTheTitlesTaggedWithTheirFirstName()
	{
		Renamed();
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"},{"id":2,"label":"sammlarr-u-renamed"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1]},{"id":11,"tmdbId":550,"tags":[2]}]""");
		EmptySonarr();
		Identifying();

		await Testee().GetAsync();

		_media
			.IdentifyAsync(Is<IEnumerable<IdentifyRequest>>(requests => requests!.Single().Candidates.Any(id => id.Value == "27205")), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WhenRadarrIsNotSetUp_ReadsSonarrAndNamesRadarrAsDown()
	{
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("""[{"id":3,"tvdbId":81189,"tags":[1]}]""");
		Identifying();
		var testee = Testee(Connected with { Radarr = ArrInstanceConfig.Empty });

		var snapshot = await testee.GetAsync();

		await Assert.That(snapshot.Ids).IsEquivalentTo([Known]);
		var down = await Assert.That(snapshot.Down).IsSome();
		await Assert.That(down.Apps).IsEquivalentTo([ArrApp.Radarr]);
		await Assert.That(down.Reason).Contains("Radarr is not set up");
	}

	[Test]
	public async Task GetAsync_WhenRadarrRefusesTheKey_SaysWhereToFixIt()
	{
		_radarr.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);
		EmptySonarr();
		Identifying();

		var snapshot = await Testee().GetAsync();

		await Assert
			.That(snapshot.Down.Map(down => down.Reason))
			.IsSome()
			.And.IsEqualTo("Radarr answered 401 Unauthorized at http://radarr:7878. Check the API key under Settings › General in Radarr.");
	}

	[Test]
	public async Task GetAsync_WhenBothAppsRefuseTheKey_NamesBoth()
	{
		_radarr.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);
		_sonarr.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);

		var failed = await Assert.That(async () => await Testee().GetAsync()).Throws<ArrAppsUnreachableException>();

		await Assert.That(failed!.Apps).IsEquivalentTo([ArrApp.Radarr, ArrApp.Sonarr]);
	}

	[Test]
	public async Task ApplyAsync_ForANewMovie_AddsItByItsTmdbIdAndStoredTitleTaggedWithTheProfileAndFolder()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("[]");
		_radarr.OnPost("/api/v3/tag").RespondWithJson("""{"id":1,"label":"sammlarr-u-guest"}""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr.OnPost("/api/v3/movie").RespondWithJson("""{"id":5,"tmdbId":27205,"tags":[1]}""");
		Addressing(TmdbId.From("27205", MediaType.Movie));
		Storing(MediaType.Movie, "Inception", "Action", "Science Fiction");

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(
			request =>
				request
					.Method(HttpMethod.Post)
					.Path("/api/v3/movie")
					.BodyContains("\"tmdbId\":27205")
					.BodyContains("\"title\":\"Inception\"")
					.BodyContains("\"qualityProfileId\":1")
					.BodyContains("\"rootFolderPath\":\"/movies\"")
					.BodyContains("\"tags\":[1]")
					.BodyContains("\"searchForMovie\":true"),
			Times.Once
		);
	}

	[Test]
	public async Task ApplyAsync_ForANewMovieWithoutAStoredTitle_LooksItUpAndAddsItFromTheLookup()
	{
		RadarrAdds(27205);
		_radarr
			.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/movie/lookup"))
			.RespondWithJson("""[{"title":"Inception","tmdbId":27205,"year":2010}]""");

		await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*/movie/lookup\\?term=tmdb:27205$"), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/movie").BodyContains("\"year\":2010"), Times.Once);
	}

	[Test]
	public async Task ApplyAsync_ForAMovieThatIsNotAnimated_AddsItWithoutALookup()
	{
		RadarrAdds(27205);
		Storing(MediaType.Movie, "Inception", "Action", "Science Fiction");
		HoldingOnAnimeSites();

		var acks = await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.PathStartsWith("/api/v3/movie/lookup"), Times.Never);
		_radarr.Verify(
			request => request.Method(HttpMethod.Post).Path("/api/v3/movie").BodyContains("\"rootFolderPath\":\"/movies\"").BodyContains("\"qualityProfileId\":1"),
			Times.Once
		);
	}

	[Test]
	[Arguments("anime", "/anime-movies")]
	[Arguments("sequel", "/movies")]
	public async Task ApplyAsync_ForAnAnimatedMovieTheMapDoesNotKnow_LooksItUpAndRoutesByRadarrsKeyword(string keyword, string folder)
	{
		RadarrLooksUp(260514, keyword);
		Storing(MediaType.Movie, "Cars 3", "Animation", "Family");
		HoldingOnAnimeSites();

		await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*/movie/lookup\\?term=tmdb:260514$"), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/movie").BodyContains($"\"rootFolderPath\":\"{folder}\""), Times.Once);
		_animeIds
			.FindManyAsync(Is<IReadOnlyCollection<IExternalMediaId>>(ids => ids!.Single().Equals(TmdbId.From("260514", MediaType.Movie))), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task ApplyAsync_ForAMovieWithoutStoredGenres_LooksItUp()
	{
		RadarrLooksUp(260514, "sequel");
		Storing(MediaType.Movie, "Cars 3");
		HoldingOnAnimeSites();

		await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*/movie/lookup\\?term=tmdb:260514$"), Times.Once);
	}

	[Test]
	public async Task ApplyAsync_ForAShowInTheAnimeMap_AddsItAsAnimeWithoutALookup()
	{
		SonarrAdds(424536);
		Storing(MediaType.Show, "Frieren", "Animation", "Fantasy");
		HoldingOnAnimeSites();
		_animeIds
			.FindManyAsync(Any(), Any())
			.Returns(
				new Dictionary<IExternalMediaId, AnimeIdMapping>
				{
					[TvdbId.From("424536", MediaType.Show)] = new(MediaType.Show, Some.Of(154587), Some.Of(52991), Some.Of(424536), new None(), new None(), new None()),
				}
			);

		var acks = await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_sonarr.Verify(request => request.PathStartsWith("/api/v3/series/lookup"), Times.Never);
		_sonarr.Verify(
			request =>
				request
					.Method(HttpMethod.Post)
					.Path("/api/v3/series")
					.BodyContains("\"tvdbId\":424536")
					.BodyContains("\"title\":\"Frieren\"")
					.BodyContains("\"rootFolderPath\":\"/anime\"")
					.BodyContains("\"seriesType\":\"anime\""),
			Times.Once
		);
	}

	[Test]
	public async Task ApplyAsync_WithoutAnAnimeFolder_NeverLooksUp()
	{
		SonarrAdds(371028);
		Storing(MediaType.Show, "Arcane", "Animation", "Action");

		await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_sonarr.Verify(request => request.PathStartsWith("/api/v3/series/lookup"), Times.Never);
		_sonarr.Verify(
			request => request.Method(HttpMethod.Post).Path("/api/v3/series").BodyContains("\"rootFolderPath\":\"/tv\"").BodyContains("\"seriesType\":\"standard\""),
			Times.Once
		);
	}

	[Test]
	public async Task ApplyAsync_WhenRadarrDoesNotKnowTheId_MarksItUnaddressable()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr
			.OnPost("/api/v3/movie")
			.RespondWith(
				new HttpResponseMessage(HttpStatusCode.BadRequest)
				{
					Content = new StringContent(
						"""[{"propertyName":"TmdbId","errorMessage":"A movie with this ID was not found. Path: ","attemptedValue":999999999,"severity":"error"}]"""
					),
				}
			);
		Addressing(TmdbId.From("999999999", MediaType.Movie));
		Storing(MediaType.Movie, "Gone from TMDB", "Drama");

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable)]);
	}

	[Test]
	public async Task ApplyAsync_ForAViewerRenamedSinceTheyJoined_TagsTheTitleWithTheirFirstName()
	{
		Renamed();
		RadarrAdds(27205);
		Storing(MediaType.Movie, "Inception");

		await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/movie").BodyContains("\"tags\":[1]"), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/tag"), Times.Never);
	}

	[Test]
	public async Task ApplyAsync_ForAFeedsTitle_TagsItWithTheFeedToo()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnPost("/api/v3/tag").RespondWithJson("""{"id":2,"label":"sammlarr-latest-movies"}""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee()
			.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, Some.Of(TestFeeds.Feed(Guid.NewGuid(), "Latest movies")))])
			.ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/tag").BodyContains("sammlarr-latest-movies"), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"tags\":[1,2]").BodyContains("\"applyTags\":\"add\""), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/movie"), Times.Never);
	}

	[Test]
	public async Task ApplyAsync_WhenTheLookupFindsNothing_AcksItUnaddressable()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/movie/lookup")).RespondWithJson("[]");
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable)]);
	}

	[Test]
	public async Task ApplyAsync_ForAShow_AddsItToSonarrByItsTvdbIdAndStoredTitle()
	{
		SonarrAdds(81189);
		Storing(MediaType.Show, "Breaking Bad", "Crime", "Drama");

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_sonarr.Verify(request => request.PathStartsWith("/api/v3/series/lookup"), Times.Never);
		_sonarr.Verify(
			request =>
				request
					.Method(HttpMethod.Post)
					.Path("/api/v3/series")
					.BodyContains("\"tvdbId\":81189")
					.BodyContains("\"title\":\"Breaking Bad\"")
					.BodyContains("\"rootFolderPath\":\"/tv\"")
					.BodyContains("\"seasonFolder\":true"),
			Times.Once
		);
		await Assert.That(_radarr.Requests).IsEmpty();
	}

	[Test]
	public async Task ApplyAsync_ForANewShowSonarrTagsAnime_AddsItToTheAnimeFolderAsAnAnimeSeries()
	{
		SonarrLooksUp(424536, "Animation", "Anime", "Fantasy");
		Storing(MediaType.Show, "Frieren", "Animation", "Fantasy");
		HoldingOnAnimeSites();

		var acks = await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_sonarr.Verify(
			request =>
				request
					.Method(HttpMethod.Post)
					.Path("/api/v3/series")
					.BodyContains("\"rootFolderPath\":\"/anime\"")
					.BodyContains("\"qualityProfileId\":4")
					.BodyContains("\"seriesType\":\"anime\""),
			Times.Once
		);
	}

	[Test]
	public async Task ApplyAsync_ForANewMovieRadarrTagsAnime_AddsItToTheAnimeFolderWithTheAnimeProfile()
	{
		RadarrLooksUp(372058, "anime", "body swap");
		Storing(MediaType.Movie, "Your Name.", "Animation", "Romance");
		HoldingOnAnimeSites();

		await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(
			request =>
				request.Method(HttpMethod.Post).Path("/api/v3/movie").BodyContains("\"rootFolderPath\":\"/anime-movies\"").BodyContains("\"qualityProfileId\":8"),
			Times.Once
		);
	}

	[Test]
	public async Task ApplyAsync_ForANewShowWithoutTheAnimeTag_AddsItToTheDefaultFolderAsAStandardSeries()
	{
		SonarrLooksUp(371028, "Action", "Animation", "Fantasy");
		Storing(MediaType.Show, "Arcane", "Animation", "Action");
		HoldingOnAnimeSites();

		await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_sonarr.Verify(
			request => request.Method(HttpMethod.Post).Path("/api/v3/series").BodyContains("\"rootFolderPath\":\"/tv\"").BodyContains("\"seriesType\":\"standard\""),
			Times.Once
		);
	}

	public static IEnumerable<Func<IExternalMediaId>> AnimeSiteIds() =>
		[() => AniListId.From("154587", MediaType.Show), () => MalId.From("52991", MediaType.Show)];

	[Test]
	[MethodDataSource(nameof(AnimeSiteIds))]
	public async Task ApplyAsync_ForAShowWhoseTitleHoldsAnAnimeSiteId_AddsItToTheAnimeFolderWithoutALookup(IExternalMediaId held)
	{
		SonarrAdds(424536);
		Storing(MediaType.Show, "Frieren", "Animation", "Fantasy");
		HoldingOnAnimeSites(held);

		await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_sonarr.Verify(request => request.PathStartsWith("/api/v3/series/lookup"), Times.Never);
		_sonarr.Verify(
			request => request.Method(HttpMethod.Post).Path("/api/v3/series").BodyContains("\"rootFolderPath\":\"/anime\"").BodyContains("\"seriesType\":\"anime\""),
			Times.Once
		);
	}

	[Test]
	public async Task ApplyAsync_WithoutAnAnimeFolder_NeverReadsTheStoredIdsOrTheAnimeMap()
	{
		SonarrLooksUp(424536, "Anime");

		await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_sonarr.Verify(
			request => request.Method(HttpMethod.Post).Path("/api/v3/series").BodyContains("\"rootFolderPath\":\"/tv\"").BodyContains("\"seriesType\":\"standard\""),
			Times.Once
		);
		_media.GetIdsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any()).WasNeverCalled();
		_animeIds.FindManyAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ApplyAsync_RemovingWhileRoutingAnime_NeverReadsTheStoredIdsDetailsOrTheAnimeMap()
	{
		EmptySonarr();
		Addressing(TvdbId.From("424536", MediaType.Show));

		await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())]).ToListAsync();

		_media.GetIdsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any()).WasNeverCalled();
		_media.GetStoredDetailsAsync(Any<IEnumerable<MediaId>>(), Any()).WasNeverCalled();
		_animeIds.FindManyAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ApplyAsync_ForAnAnimeTitleAlreadyInTheApp_OnlyRetagsItWhereItIs()
	{
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("""[{"id":3,"tvdbId":424536,"tags":[1]}]""");
		_sonarr.OnPut("/api/v3/series/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TvdbId.From("424536", MediaType.Show));
		HoldingOnAnimeSites(AniListId.From("154587", MediaType.Show));

		var acks = await Testee(RoutingAnime).ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_sonarr.Verify(request => request.Method(HttpMethod.Put).Path("/api/v3/series/editor"), Times.Once);
		_sonarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/series"), Times.Never);
		_sonarr.Verify(request => request.PathStartsWith("/api/v3/series/lookup"), Times.Never);
	}

	[Test]
	public async Task ApplyAsync_RemovingATitleOnlyThisUserWants_DeletesItAndKeepsItsFiles()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"},{"id":2,"label":"sammlarr-latest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1,2]}]""");
		_radarr.OnRequest(request => request.Method(HttpMethod.Delete).PathStartsWith("/api/v3/movie/10")).Respond(HttpStatusCode.OK);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.Method(HttpMethod.Delete).PathMatches(".*/movie/10\\?deleteFiles=false&addImportExclusion=false$"), Times.Once);
	}

	[Test]
	public async Task ApplyAsync_RemovingATitleAnotherUserWants_OnlyTakesThisUsersTagOff()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"},{"id":3,"label":"sammlarr-u-daerup"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1,3]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"tags\":[1]").BodyContains("\"applyTags\":\"remove\""), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Delete), Times.Never);
	}

	[Test]
	public async Task ApplyAsync_ForATitleWithoutAnAppId_AcksItUnaddressableWithoutCallingEitherApp()
	{
		_media.GetSinkIdsAsync(Any<IEnumerable<MediaId>>(), Any()).Returns(new Dictionary<MediaId, IExternalMediaId>());

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert
			.That(acks)
			.IsEquivalentTo([
				new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable)
				{
					Reason = Some.Of("Sammlarr has no TMDB or TVDB id for this title, the ids Radarr and Sonarr add by."),
				},
			]);
		await Assert.That(_radarr.Requests).IsEmpty();
		await Assert.That(_sonarr.Requests).IsEmpty();
	}

	[Test]
	public async Task ApplyAsync_ForAnIdNeitherAppTakes_AcksItUnaddressableWithoutCallingEitherApp()
	{
		Addressing(TmdbId.From("1396", MediaType.Show));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert
			.That(acks)
			.IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable) { Reason = Some.Of("Neither Radarr nor Sonarr adds a show by its TMDB id.") }]);
		await Assert.That(_radarr.Requests).IsEmpty();
		await Assert.That(_sonarr.Requests).IsEmpty();
	}

	[Test]
	public async Task ApplyAsync_WhenRadarrIsNotSetUp_AcksItFailed()
	{
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee(Connected with { Radarr = ArrInstanceConfig.Empty })
			.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())])
			.ToListAsync();

		await Assert
			.That(acks)
			.IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed) { Reason = Some.Of("Radarr is not set up: it needs an address and an API key.") }]);
	}

	[Test]
	public async Task GetAsync_WhenUserTagsAreOff_ReadsEveryTitleCarryingAnyOfSammlarrsTags()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr"},{"id":2,"label":"sammlarr-latest"},{"id":3,"label":"4k"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[2]},{"id":11,"tmdbId":550,"tags":[3]}]""");
		EmptySonarr();
		Identifying();

		var snapshot = await Testee(Connected with { Tags = ArrTagScheme.Default with { PerUser = false } }).GetAsync();

		await Assert.That(snapshot.Ids).IsEquivalentTo([Known]);
		_media
			.IdentifyAsync(Is<IEnumerable<IdentifyRequest>>(requests => requests!.Single().Candidates.Any(id => id.Value == "27205")), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task ApplyAsync_RemovingWhenUserTagsAreOff_DeletesTheTitle()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr"},{"id":3,"label":"sammlarr-u-daerup"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1,3]}]""");
		_radarr.OnRequest(request => request.Method(HttpMethod.Delete).PathStartsWith("/api/v3/movie/10")).Respond(HttpStatusCode.OK);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee(Connected with { Tags = ArrTagScheme.Default with { PerUser = false } })
			.ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())])
			.ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.Method(HttpMethod.Delete), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Put), Times.Never);
	}

	[Test]
	public async Task ApplyAsync_RemovingAShow_DeletesItWithoutAnImportListExclusion()
	{
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("""[{"id":3,"tvdbId":81189,"tags":[1]}]""");
		_sonarr.OnRequest(request => request.Method(HttpMethod.Delete).PathStartsWith("/api/v3/series/3")).Respond(HttpStatusCode.OK);
		Addressing(TvdbId.From("81189", MediaType.Show));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_sonarr.Verify(request => request.Method(HttpMethod.Delete).PathMatches(".*/series/3\\?deleteFiles=false&addImportListExclusion=false$"), Times.Once);
	}

	[Test]
	public async Task ApplyAsync_ForANewMovieWithoutAProfileOrFolderChosen_AcksItFailedWithoutAddingIt()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee(Connected with { Radarr = Connected.Radarr with { RootFolderPath = new None() } })
			.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())])
			.ToListAsync();

		await Assert
			.That(acks)
			.IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed) { Reason = Some.Of("Radarr has no quality profile or root folder chosen yet.") }]);
		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/movie"), Times.Never);
	}

	[Test]
	public async Task ApplyAsync_WhenRadarrRefusesTheAdd_AcksItFailedWithRadarrsMessage()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr
			.OnPost("/api/v3/movie")
			.RespondWith(
				new HttpResponseMessage(HttpStatusCode.BadRequest)
				{
					Content = new StringContent("""[{"propertyName":"TmdbId","errorMessage":"This movie has already been added"}]"""),
				}
			);
		Addressing(TmdbId.From("27205", MediaType.Movie));
		Storing(MediaType.Movie, "Inception");

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert
			.That(acks)
			.IsEquivalentTo([
				new SinkSyncAck(Known, SinkSyncOutcome.Failed)
				{
					Reason = Some.Of("Radarr answered 400 BadRequest at http://radarr:7878. This movie has already been added"),
				},
			]);
	}

	[Test]
	public async Task ApplyAsync_WhenRadarrRefusesTheKey_FailsTheWholeBatchOnOneAttempt()
	{
		var other = MediaId.New();
		_radarr.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);
		_media
			.GetSinkIdsAsync(Any<IEnumerable<MediaId>>(), Any())
			.Returns(new Dictionary<MediaId, IExternalMediaId> { [Known] = TmdbId.From("27205", MediaType.Movie), [other] = TmdbId.From("550", MediaType.Movie) });

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None()), new SinkPush(other, new None())]).ToListAsync();

		Optional<string> reason = Some.Of("Radarr answered 401 Unauthorized at http://radarr:7878. Check the API key under Settings › General in Radarr.");
		await Assert
			.That(acks)
			.IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed) { Reason = reason }, new SinkSyncAck(other, SinkSyncOutcome.Failed) { Reason = reason }]);
		_radarr.Verify(request => request.Path("/api/v3/tag"), Times.Once);
	}

	[Test]
	public async Task GetAsync_AfterUserTagsMovedFromNamesToIds_StillReadsTheTitlesTaggedByName()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1]}]""");
		EmptySonarr();
		Identifying();

		var snapshot = await Testee(Connected with { Tags = ArrTagScheme.Default with { UserSource = ArrUserTagSource.Id } }).GetAsync();

		await Assert.That(snapshot.Ids).IsEquivalentTo([Known]);
	}

	[Test]
	public async Task GetAsync_ForTheFirstUserOnceUserTagsAreOn_ClaimsTitlesTaggedWhileTheyWereOff()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr"},{"id":2,"label":"sammlarr-u-daerup"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1]},{"id":11,"tmdbId":550,"tags":[2]}]""");
		EmptySonarr();
		Identifying();

		var snapshot = await Testee().GetAsync();

		await Assert.That(snapshot.Ids).IsEquivalentTo([Known]);
		_media
			.IdentifyAsync(Is<IEnumerable<IdentifyRequest>>(requests => requests!.Single().Candidates.Any(id => id.Value == "27205")), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_ForALaterUser_LeavesTitlesWithoutAUserTagToTheFirst()
	{
		_users
			.GetAllAsync(Any())
			.Returns([
				new User(UserId.New(), "Daerup", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch),
				new User(Guest, "Guest", string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch.AddDays(1)),
			]);
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1]}]""");
		EmptySonarr();
		Identifying();

		var snapshot = await Testee().GetAsync();

		await Assert.That(snapshot.Ids).IsEmpty();
	}

	[Test]
	public async Task ApplyAsync_AddingATitleAlreadyInTheAppWithoutSammlarrsTags_MarksItProtected()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnPost("/api/v3/tag").RespondWithJson("""{"id":4,"label":"sammlarr-protected"}""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"tags\":[1,4]"), Times.Once);
	}

	[Test]
	public async Task ApplyAsync_RemovingAProtectedTitle_TakesSammlarrsTagsOffInsteadOfDeletingIt()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"},{"id":4,"label":"sammlarr-protected"},{"id":7,"label":"4k"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1,4,7]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"tags\":[1,4]").BodyContains("\"applyTags\":\"remove\""), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Delete), Times.Never);
	}

	[Test]
	public async Task ApplyAsync_RemovingATitleWithTheOldKeptTag_DeletesItAsNoLongerProtected()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"},{"id":4,"label":"sammlarr-kept"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1,4]}]""");
		_radarr.OnRequest(request => request.Method(HttpMethod.Delete).PathStartsWith("/api/v3/movie/10")).Respond(HttpStatusCode.OK);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		await Testee().ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Delete).PathMatches(".*/movie/10\\?deleteFiles=false&addImportExclusion=false$"), Times.Once);
	}

	[Test]
	public async Task ApplyAsync_AddingATitleTheFirstUserHoldsWithoutAUserTag_StampsTheirTagToo()
	{
		_users
			.GetAllAsync(Any())
			.Returns([
				new User(UserId.New(), "Daerup", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch),
				new User(Guest, "Guest", string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch.AddDays(1)),
			]);
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr"}]""");
		_radarr
			.OnRequest(request => request.Method(HttpMethod.Post).Path("/api/v3/tag").BodyContains("sammlarr-u-guest"))
			.RespondWithJson("""{"id":2,"label":"sammlarr-u-guest"}""");
		_radarr
			.OnRequest(request => request.Method(HttpMethod.Post).Path("/api/v3/tag").BodyContains("sammlarr-u-daerup"))
			.RespondWithJson("""{"id":3,"label":"sammlarr-u-daerup"}""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"tags\":[2,3]"), Times.Once);
	}

	private static readonly User Mira = new(
		UserId.From(Guid.Parse("1c5d7a4e-2f10-4b8e-9f3a-6d2e8c1b0a77")),
		"Mira",
		string.Empty,
		UserRole.Member,
		DateTimeOffset.UnixEpoch
	);

	[Test]
	public async Task HoldingsOfAsync_CountsTheTitlesOnlyTheirTagHoldsApartFromSharedAndProtectedOnes()
	{
		_radarr
			.OnGet("/api/v3/tag")
			.RespondWithJson("""[{"id":1,"label":"sammlarr-u-mira"},{"id":2,"label":"sammlarr-u-guest"},{"id":3,"label":"sammlarr-protected"}]""");
		_radarr
			.OnGet("/api/v3/movie")
			.RespondWithJson(
				"""[{"id":10,"tmdbId":1,"tags":[1]},{"id":11,"tmdbId":2,"tags":[1,2]},{"id":12,"tmdbId":3,"tags":[1,3]},{"id":13,"tmdbId":4,"tags":[2]}]"""
			);
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":5,"label":"sammlarr-u-mira"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("""[{"id":20,"tvdbId":81189,"tags":[5]}]""");

		var holdings = await Testee().HoldingsOfAsync(Mira);

		await Assert.That(holdings).IsSuccess().And.IsEqualTo(new SinkHoldings(2, 2, "sammlarr-u-mira"));
	}

	[Test]
	public async Task HoldingsOfAsync_ForAPersonRenamedSinceTheyJoined_CountsTheTitlesTaggedWithTheirFirstName()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-mira"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":1,"tags":[1]}]""");
		EmptySonarr();

		var holdings = await Testee().HoldingsOfAsync(Mira with { DisplayName = "Mira Renamed" });

		await Assert.That(holdings).IsSuccess().And.IsEqualTo(new SinkHoldings(1, 0, "sammlarr-u-mira"));
	}

	[Test]
	public async Task DeleteTitlesOfAsync_DeletesOnlyTheirSoleTitlesInOneEditorCallPerApp()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-mira"},{"id":2,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":1,"tags":[1]},{"id":11,"tmdbId":2,"tags":[1,2]},{"id":12,"tmdbId":3,"tags":[1]}]""");
		_radarr.OnRequest(request => request.Method(HttpMethod.Delete).Path("/api/v3/movie/editor")).Respond(HttpStatusCode.OK);
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		EmptySonarr();

		var deleted = await Testee().DeleteTitlesOfAsync(Mira);

		await Assert.That(deleted).IsSuccess().And.IsEqualTo(2);
		_radarr.Verify(
			request =>
				request
					.Method(HttpMethod.Delete)
					.Path("/api/v3/movie/editor")
					.BodyContains("\"movieIds\":[10,12]")
					.BodyContains("\"deleteFiles\":false")
					.BodyContains("\"addImportExclusion\":false"),
			Times.Once
		);
		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"movieIds\":[11]"), Times.Once);
		_sonarr.Verify(request => request.Method(HttpMethod.Delete), Times.Never);
	}

	[Test]
	public async Task DeleteTitlesOfAsync_TakesTheirTagOffTitlesThatStayIncludingProtectedOnes()
	{
		_radarr
			.OnGet("/api/v3/tag")
			.RespondWithJson("""[{"id":1,"label":"sammlarr-u-mira"},{"id":2,"label":"sammlarr-u-guest"},{"id":3,"label":"sammlarr-protected"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":11,"tmdbId":2,"tags":[1,2]},{"id":12,"tmdbId":3,"tags":[1,3]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		EmptySonarr();

		var deleted = await Testee().DeleteTitlesOfAsync(Mira);

		await Assert.That(deleted).IsSuccess().And.IsEqualTo(0);
		_radarr.Verify(request => request.Method(HttpMethod.Delete), Times.Never);
		_radarr.Verify(
			request => request.Method(HttpMethod.Put).BodyContains("\"movieIds\":[11,12]").BodyContains("\"tags\":[1]").BodyContains("\"applyTags\":\"remove\""),
			Times.Once
		);
	}

	[Test]
	public async Task DeleteTitlesOfAsync_ForTwoPeopleWhoseNamesMakeTheSameTag_DeletesNothingOfTheOthers()
	{
		var bob = Mira with { TagName = "bob.smith" };
		_users.GetAllAsync(Any()).Returns([bob, new User(Guest, "bob_smith", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch)]);
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-bob-smith"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":1,"tags":[1]}]""");
		EmptySonarr();

		var holdings = await Testee().HoldingsOfAsync(bob);
		var deleted = await Testee().DeleteTitlesOfAsync(bob);

		await Assert.That(holdings).IsSuccess().And.IsEqualTo(new SinkHoldings(0, 1, "sammlarr-u-bob-smith"));
		await Assert.That(deleted).IsSuccess().And.IsEqualTo(0);
		_radarr.Verify(request => request.Method(HttpMethod.Delete), Times.Never);
		_radarr.Verify(request => request.Method(HttpMethod.Put), Times.Never);
	}

	[Test]
	public async Task DeleteTitlesOfAsync_WhenSonarrFailsAfterRadarr_SaysWhatWasAlreadyDeleted()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-mira"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":1,"tags":[1]},{"id":12,"tmdbId":3,"tags":[1]}]""");
		_radarr.OnRequest(request => request.Method(HttpMethod.Delete).Path("/api/v3/movie/editor")).Respond(HttpStatusCode.OK);
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":5,"label":"sammlarr-u-mira"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("""[{"id":20,"tvdbId":81189,"tags":[5]}]""");
		_sonarr.OnRequest(request => request.Method(HttpMethod.Delete)).Respond(HttpStatusCode.InternalServerError);

		var deleted = await Testee().DeleteTitlesOfAsync(Mira);

		await Assert.That(deleted).IsError().And.Multiple(async error => await Assert.That(error.Reason).EndsWith("2 of their titles were already deleted."));
	}

	[Test]
	public async Task HoldingsOfAsync_WithoutPerPersonTags_SaysTheTitlesCannotBeToldApart()
	{
		var holdings = await Testee(Connected with { Tags = ArrTagScheme.Default with { PerUser = false } }).HoldingsOfAsync(Mira);

		await Assert.That(holdings).IsError();
		_radarr.Verify(request => request.Method(HttpMethod.Get), Times.Never);
	}

	private ArrSink Testee() => Testee(Connected);

	private ArrSink Testee(ArrSinkConfig config)
	{
		var configs = IArrSinkConfigService.Mock();
		configs.GetAsync(Any()).Returns(config);

		return new ArrSink(
			new ArrApis(new RadarrApi(new HttpClient(_radarr), new RadarrOptions()), new SonarrApi(new HttpClient(_sonarr), new SonarrOptions())),
			configs.Object,
			_users.Object,
			_media.Object,
			_animeIds.Object,
			_viewer.Object,
			NullLogger<ArrSink>.Instance
		);
	}

	private void Renamed()
	{
		_users.GetAsync(Guest, Any()).Returns(GuestUser with { DisplayName = "Renamed" });
		_viewer.DisplayName.Getter.Returns("Renamed");
	}

	private void EmptySonarr()
	{
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("[]");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("[]");
	}

	private void Identifying()
	{
		_media
			.IdentifyAsync(Any<IEnumerable<IdentifyRequest>>(), Any())
			.Returns(
				(requests, _) =>
					requests
						.Select(request => (Result<MediaIdentity>)Success.Of(new MediaIdentity(Known, [.. request.Candidates.OfType<IIdentityKey>()])))
						.ToAsyncEnumerable()
			);
	}

	private void Addressing(IExternalMediaId id)
	{
		_media.GetSinkIdsAsync(Any<IEnumerable<MediaId>>(), Any()).Returns(new Dictionary<MediaId, IExternalMediaId> { [Known] = id });
	}

	private void Storing(MediaType type, string title, params string[] genres)
	{
		_media
			.GetStoredDetailsAsync(Any<IEnumerable<MediaId>>(), Any())
			.Returns(new Dictionary<MediaId, MediaDetails> { [Known] = MediaDetails.Empty(type) with { Title = title, Genres = [.. genres] } });
	}

	private void RadarrAdds(int tmdb)
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr.OnPost("/api/v3/movie").RespondWithJson($$"""{"id":5,"tmdbId":{{tmdb}},"tags":[1]}""");
		Addressing(TmdbId.From($"{tmdb}", MediaType.Movie));
	}

	private void SonarrAdds(int tvdb)
	{
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("[]");
		_sonarr.OnPost("/api/v3/series").RespondWithJson($$"""{"id":3,"tvdbId":{{tvdb}},"tags":[1]}""");
		Addressing(TvdbId.From($"{tvdb}", MediaType.Show));
	}

	private void SonarrLooksUp(int tvdb, params string[] genres)
	{
		var show = new JsonObject { ["tvdbId"] = tvdb, ["genres"] = new JsonArray([.. genres.Select(genre => JsonValue.Create(genre))]) };
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("[]");
		_sonarr.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/series/lookup")).RespondWithJson($"[{show.ToJsonString()}]");
		_sonarr.OnPost("/api/v3/series").RespondWithJson($$"""{"id":3,"tvdbId":{{tvdb}},"tags":[1]}""");
		Addressing(TvdbId.From($"{tvdb}", MediaType.Show));
	}

	private void RadarrLooksUp(int tmdb, params string[] keywords)
	{
		var movie = new JsonObject { ["tmdbId"] = tmdb, ["keywords"] = new JsonArray([.. keywords.Select(keyword => JsonValue.Create(keyword))]) };
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/movie/lookup")).RespondWithJson($"[{movie.ToJsonString()}]");
		_radarr.OnPost("/api/v3/movie").RespondWithJson($$"""{"id":5,"tmdbId":{{tmdb}},"tags":[1]}""");
		Addressing(TmdbId.From($"{tmdb}", MediaType.Movie));
	}

	private void HoldingOnAnimeSites(params IExternalMediaId[] held)
	{
		_media.GetIdsAsync(Any<IEnumerable<MediaId>>(), MediaProvider.AniList, Any()).Returns(Held(held, MediaProvider.AniList));
		_media.GetIdsAsync(Any<IEnumerable<MediaId>>(), MediaProvider.Mal, Any()).Returns(Held(held, MediaProvider.Mal));
	}

	private static Dictionary<MediaId, IExternalMediaId> Held(IExternalMediaId[] held, MediaProvider site) =>
		held.Where(id => id.Source == site).ToDictionary(_ => Known);
}
