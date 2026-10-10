using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Core.Wantlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SinkSwitchTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WantlistItemProvenance ByHand = new(new ManualOrigin(ManualSource.Search));

	private static readonly ArrInstanceConfig SetUp = ArrInstanceConfig.Empty with
	{
		Url = Some.Of("http://radarr:7878"),
		ApiKey = Some.Of("0123456789abcdef0123456789abcdef"),
		QualityProfileId = Some.Of(4),
		RootFolderPath = Some.Of("/media"),
	};

	private ISinkSwitch Testee => host.Resolve<ISinkSwitch>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Before(Test)]
	public async Task Background()
	{
		await host.EnsurePlexAccount();
		await host.Resolve<IArrSinkConfigService>().StoreAsync(new ArrSinkConfig(SetUp, SetUp, ArrTagScheme.Default));
	}

	[Test]
	public async Task SwitchAsync_ToRadarrAndSonarr_GivesEachWantlistTitleTheIdTheAppsAddByAndQueuesItForTheFirstSync()
	{
		host.PlexHolds(TestMedia.FightClub);
		var movie = await host.Movie(new MediaSpec("Fight Club", MediaType.Movie, [Plex("550")])).OnWantlist(ByHand);

		var switched = await Testee.SwitchAsync(SinkKind.Arr);

		await Assert.That(switched).IsSuccess().And.IsEqualTo(new SinkSwitched(1, 0));
		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).Sink).IsEqualTo(SinkKind.Arr);
		await Assert.That((await host.Resolve<IMediaService>().GetSinkIdsAsync([movie]))[movie]).IsEqualTo(TmdbId.From("550", MediaType.Movie));
		await Assert.That((await host.Resolve<IWantlistService>().GetOutboxAsync()).Adds).ContainsKey(movie);
	}

	[Test]
	public async Task SwitchAsync_ForATitleNoProviderCanConvert_CountsItAndStillSwitches()
	{
		var lost = new MediaSpec("Lost", MediaType.Movie, [Plex("999")]);
		host.PlexHasNoMetadataFor(lost);
		await host.Movie(lost).OnWantlist(ByHand);

		var switched = await Testee.SwitchAsync(SinkKind.Arr);

		await Assert.That(switched).IsSuccess().And.IsEqualTo(new SinkSwitched(1, 1));
		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).Sink).IsEqualTo(SinkKind.Arr);
	}

	[Test]
	public async Task SwitchAsync_WhileAWantlistSyncRuns_RefusesAndKeepsTheSink()
	{
		var running = await host.Resolve<WantlistSyncGate>().EnterAsync(TestCurrentUser.DefaultId);

		var switched = await Testee.SwitchAsync(SinkKind.Arr);

		running.Apply(lease => lease.Dispose());
		await Assert.That(switched).IsError();
		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).Sink).IsEqualTo(SinkKind.Plex);
	}

	[Test]
	public async Task SwitchAsync_ForEveryUser_QueuesTheirOwnWantlist()
	{
		await host.Movie(new MediaSpec("Fight Club", MediaType.Movie, [Tmdb("550")])).OnWantlist(ByHand);
		var other = await host.SeedExistingUser("daerup", UserRole.Member, "Daerup");
		host.CurrentUser.Id = other;
		var theirs = await host.Movie(new MediaSpec("The Matrix", MediaType.Movie, [Tmdb("603")])).OnWantlist(ByHand);

		host.CurrentUser.Id = TestCurrentUser.DefaultId;
		var switched = await Testee.SwitchAsync(SinkKind.Arr);

		await Assert.That(switched).IsSuccess().And.IsEqualTo(new SinkSwitched(2, 0));
		host.CurrentUser.Id = other;
		await Assert.That((await host.Resolve<IWantlistService>().GetOutboxAsync()).Adds.Keys).IsEquivalentTo([theirs]);
	}

	[Test]
	public async Task SwitchAsync_BackToPlex_GivesEachTitleItsPlexIdAndQueuesIt()
	{
		host.PlexHolds(TestMedia.FightClub);
		await host.TmdbDescribes(TestMedia.FightClub);
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);
		var movie = await host.Movie(new MediaSpec("Fight Club", MediaType.Movie, [Tmdb("550")])).OnWantlist(ByHand);
		await Testee.SwitchAsync(SinkKind.Arr);

		var switched = await Testee.SwitchAsync(SinkKind.Plex);

		await Assert.That(switched).IsSuccess().And.IsEqualTo(new SinkSwitched(1, 0));
		await Assert.That((await host.Resolve<IMediaService>().GetSinkIdsAsync([movie]))[movie].Source).IsEqualTo(MediaProvider.Plex);
	}

	[Test]
	public async Task SwitchAsync_ToRadarrAndSonarrBeforeBothAreSetUp_RefusesAndKeepsTheSink()
	{
		await host.Resolve<IArrSinkConfigService>().StoreAsync(new ArrSinkConfig(SetUp, SetUp with { RootFolderPath = new None() }, ArrTagScheme.Default));

		var switched = await Testee.SwitchAsync(SinkKind.Arr);

		await Assert.That(switched).IsError();
		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).Sink).IsEqualTo(SinkKind.Plex);
	}

	[Test]
	public async Task MoveAsync_ToAnotherRadarr_StoresTheNewAddressAndQueuesEveryTitleForIt()
	{
		var movie = await host.Movie(new MediaSpec("Fight Club", MediaType.Movie, [Tmdb("550")])).OnWantlist(ByHand);
		await Testee.SwitchAsync(SinkKind.Arr);
		var moved = new ArrSinkConfig(SetUp with { Url = Some.Of("http://radarr.lan:7878") }, SetUp, ArrTagScheme.Default);

		var switched = await Testee.MoveAsync(moved);

		await Assert.That(switched).IsSuccess().And.IsEqualTo(new SinkSwitched(1, 0));
		await Assert.That((await host.Resolve<IArrSinkConfigService>().GetAsync()).Radarr.Url).IsSome().And.IsEqualTo("http://radarr.lan:7878");
		await Assert.That((await host.Resolve<IWantlistService>().GetOutboxAsync()).Adds).ContainsKey(movie);
	}

	[Test]
	public async Task MoveAsync_WhileAWantlistSyncRuns_KeepsTheOldAddress()
	{
		await Testee.SwitchAsync(SinkKind.Arr);
		var running = await host.Resolve<WantlistSyncGate>().EnterAsync(TestCurrentUser.DefaultId);

		var switched = await Testee.MoveAsync(new ArrSinkConfig(SetUp with { Url = Some.Of("http://radarr.lan:7878") }, SetUp, ArrTagScheme.Default));

		running.Apply(lease => lease.Dispose());
		await Assert.That(switched).IsError();
		await Assert.That((await host.Resolve<IArrSinkConfigService>().GetAsync()).Radarr.Url).IsSome().And.IsEqualTo("http://radarr:7878");
	}

	[Test]
	public async Task MoveAsync_WhileTheWantlistSyncsToPlex_RefusesAndStoresNothing()
	{
		var switched = await Testee.MoveAsync(new ArrSinkConfig(SetUp with { Url = Some.Of("http://radarr.lan:7878") }, SetUp, ArrTagScheme.Default));

		await Assert.That(switched).IsError();
		await Assert.That((await host.Resolve<IArrSinkConfigService>().GetAsync()).Radarr.Url).IsSome().And.IsEqualTo("http://radarr:7878");
	}

	[Test]
	public async Task PreviewAsync_ToRadarrAndSonarr_CountsEveryonesTitlesWithAStoredIdAsReadyAndLooksNothingUp()
	{
		host.PlexHolds(TestMedia.FightClub);
		await host.Movie(new MediaSpec("Inception", MediaType.Movie, [Tmdb("27205")])).OnWantlist(ByHand);
		var convertible = await host.Movie(new MediaSpec("Fight Club", MediaType.Movie, [Plex("550")])).OnWantlist(ByHand);
		host.CurrentUser.Id = await host.SeedExistingUser("daerup", UserRole.Member, "Daerup");
		await host.Movie(new MediaSpec("The Matrix", MediaType.Movie, [Tmdb("603")])).OnWantlist(ByHand);
		host.CurrentUser.Id = TestCurrentUser.DefaultId;

		var preview = await Testee.PreviewAsync(SinkKind.Arr);

		await Assert.That(preview).IsEqualTo(new SinkSwitchPreview(2, 1));
		await Assert.That(await host.Resolve<IMediaService>().GetSinkIdsAsync([convertible], SinkKind.Arr)).IsEmpty();
		await Assert.That((await host.Resolve<IProviderConfigService>().GetAsync()).Sink).IsEqualTo(SinkKind.Plex);
	}

	[Test]
	public async Task PreviewAsync_AsAMember_Refuses()
	{
		host.CurrentUser.Role = UserRole.Member;

		await Assert.That(async () => await Testee.PreviewAsync(SinkKind.Arr)).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task SwitchAsync_AsAMember_Refuses()
	{
		host.CurrentUser.Role = UserRole.Member;

		await Assert.That(async () => await Testee.SwitchAsync(SinkKind.Arr)).Throws<UnauthorizedAccessException>();
	}
}
