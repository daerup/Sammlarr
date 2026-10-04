using System.Net;
using API.Arr;
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

	private static readonly UserId Guest = UserId.From(Guid.Parse("8f14e45f-ceea-467a-9575-6c0f6e3b4c11"));

	[Before(Test)]
	public void Everyone() => _users.GetAllAsync(Any()).Returns([new User(Guest, "Guest", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch)]);

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
	public async Task GetAsync_WhenRadarrIsNotSetUp_FailsSayingSo()
	{
		var testee = Testee(Connected with { Radarr = ArrInstanceConfig.Empty });

		await Assert.That(async () => await testee.GetAsync()).Throws<InvalidOperationException>().WithMessageContaining("Radarr is not set up");
	}

	[Test]
	public async Task GetAsync_WhenRadarrRefusesTheKey_SaysWhereToFixIt()
	{
		_radarr.OnAnyRequest().Respond(HttpStatusCode.Unauthorized);

		await Assert
			.That(async () => await Testee().GetAsync())
			.Throws<InvalidOperationException>()
			.WithMessage("Radarr answered 401 Unauthorized at http://radarr:7878. Check the API key under Settings › General in Radarr.");
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
	public async Task ApplyAsync_AddingATitleAlreadyInTheAppWithoutSammlarrsTags_MarksItKept()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"}]""");
		_radarr.OnPost("/api/v3/tag").RespondWithJson("""{"id":4,"label":"sammlarr-kept"}""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		await Testee().ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"tags\":[1,4]"), Times.Once);
	}

	[Test]
	public async Task ApplyAsync_RemovingAKeptTitle_TakesSammlarrsTagsOffInsteadOfDeletingIt()
	{
		_radarr.OnGet("/api/v3/tag").RespondWithJson("""[{"id":1,"label":"sammlarr-u-guest"},{"id":4,"label":"sammlarr-kept"},{"id":7,"label":"4k"}]""");
		_radarr.OnGet("/api/v3/movie").RespondWithJson("""[{"id":10,"tmdbId":27205,"tags":[1,4,7]}]""");
		_radarr.OnPut("/api/v3/movie/editor").Respond(HttpStatusCode.Accepted);
		Addressing(TmdbId.From("27205", MediaType.Movie));

		var acks = await Testee().ApplyAsync(WantlistOperationAction.Remove, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Applied)]);
		_radarr.Verify(request => request.Method(HttpMethod.Put).BodyContains("\"tags\":[1,4]").BodyContains("\"applyTags\":\"remove\""), Times.Once);
		_radarr.Verify(request => request.Method(HttpMethod.Delete), Times.Never);
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

	private ArrSink Testee() => Testee(Connected);

	private ArrSink Testee(ArrSinkConfig config)
	{
		var configs = IArrSinkConfigService.Mock();
		configs.GetAsync(Any()).Returns(config);
		var user = ICurrentUser.Mock();
		user.Id.Getter.Returns(Guest);
		user.DisplayName.Getter.Returns("Guest");
		return new ArrSink(
			new ArrClients(new RadarrClient(new HttpClient(_radarr)), new SonarrClient(new HttpClient(_sonarr))),
			configs.Object,
			_users.Object,
			_media.Object,
			user.Object,
			NullLogger<ArrSink>.Instance
		);
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
