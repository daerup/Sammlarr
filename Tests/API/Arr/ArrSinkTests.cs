using System.Net;
using API.Arr;
using API.Arr.Radarr;
using API.Arr.Sonarr;
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

	private readonly MockHttpHandler _radarr = Mock.HttpHandler();

	private readonly MockHttpHandler _sonarr = Mock.HttpHandler();

	private readonly Mock<IMediaService> _media = IMediaService.Mock();

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
	public async Task ApplyAsync_ForANewMovie_LooksItUpAndAddsItTaggedWithTheProfileAndFolder()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("[]");
		_radarr.OnPost("/api/v3/tag").RespondWithJson("""{"id":1,"label":"sammlarr-u-guest"}""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr
			.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/movie/lookup"))
			.RespondWithJson("""[{"title":"Inception","tmdbId":27205}]""");
		_radarr.OnPost("/api/v3/movie").RespondWithJson("""{"id":5,"tmdbId":27205,"tags":[1]}""");
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*/movie/lookup\\?term=tmdb:27205$"), Times.Once);
		_radarr.Verify(
			request =>
				request
					.Method(HttpMethod.Post)
					.Path("/api/v3/movie")
					.BodyContains("\"qualityProfileId\":1")
					.BodyContains("\"rootFolderPath\":\"/movies\"")
					.BodyContains("\"tags\":[1]")
					.BodyContains("\"searchForMovie\":true"),
			Times.Once
		);
	}

	[Test]
	public async Task ApplyAsync_ForAViewerRenamedSinceTheyJoined_TagsTheTitleWithTheirFirstName()
	{
		Renamed();
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr
			.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/movie/lookup"))
			.RespondWithJson("""[{"title":"Inception","tmdbId":27205}]""");
		_radarr.OnPost("/api/v3/movie").RespondWithJson("""{"id":5,"tmdbId":27205,"tags":[1]}""");
		Addressing(TmdbId.From("27205", MediaType.Movie));

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
	public async Task ApplyAsync_WhenTheAppCannotFindTheTitle_AcksItUnaddressable()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("[]");
		_radarr.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/movie/lookup")).RespondWithJson("[]");
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable)]);
	}

	[Test]
	public async Task ApplyAsync_ForAShow_AddsItToSonarrByItsTvdbId()
	{
		_sonarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_sonarr.OnGet("/api/v3/series").RespondWithJson("[]");
		_sonarr
			.OnRequest(request => request.Method(HttpMethod.Get).PathStartsWith("/api/v3/series/lookup"))
			.RespondWithJson("""[{"title":"Breaking Bad","tvdbId":81189}]""");
		_sonarr.OnPost("/api/v3/series").RespondWithJson("""{"id":3,"tvdbId":81189,"tags":[1]}""");
		Addressing(TvdbId.From("81189", MediaType.Show));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_sonarr.Verify(request => request.Method(HttpMethod.Get).PathMatches(".*/series/lookup\\?term=tvdb:81189$"), Times.Once);
		_sonarr.Verify(
			request => request.Method(HttpMethod.Post).Path("/api/v3/series").BodyContains("\"rootFolderPath\":\"/tv\"").BodyContains("\"seasonFolder\":true"),
			Times.Once
		);
		await Assert.That(_radarr.Requests).IsEmpty();
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

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable)]);
		await Assert.That(_radarr.Requests).IsEmpty();
		await Assert.That(_sonarr.Requests).IsEmpty();
	}

	[Test]
	public async Task ApplyAsync_ForAnIdNeitherAppTakes_AcksItUnaddressableWithoutCallingEitherApp()
	{
		Addressing(TmdbId.From("1396", MediaType.Show));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable)]);
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

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed)]);
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

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed)]);
		_radarr.Verify(request => request.Method(HttpMethod.Post).Path("/api/v3/movie"), Times.Never);
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

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed), new SinkSyncAck(other, SinkSyncOutcome.Failed)]);
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
				(requests, _) => requests.Select(request => (Result<MediaIdentity>)Success.Of(new MediaIdentity(Known, [.. request.Candidates]))).ToAsyncEnumerable()
			);
	}

	private void Addressing(IExternalMediaId id)
	{
		_media.GetSinkIdsAsync(Any<IEnumerable<MediaId>>(), Any()).Returns(new Dictionary<MediaId, IExternalMediaId> { [Known] = id });
	}
}
