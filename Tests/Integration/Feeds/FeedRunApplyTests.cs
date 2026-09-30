using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunApplyTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IFeedRunEngine Testee => host.Resolve<IFeedRunEngine>();

	[Test]
	public async Task RunAsync_ForAnAppendFeed_AddsWhatTheListHoldsAndRemovesNothing()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("append-list", new Append());
		host.MdblistFeedContains("append-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		var stale = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));

		var result = await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var completed = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(completed!.Delta.Drops).IsEmpty();
		await Assert.That(await WantlistIdsAsync()).Contains(stale);
	}

	[Test]
	public async Task RunAsync_ForAMirrorFeed_RemovesOnlyTheTitlesItOwns()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("mirror-list", new Mirror(1));
		host.MdblistFeedContains("mirror-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();

		var ownedButGone = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));
		var addedByHand = await host.Movie(TestMedia.Interstellar).OnWantlist(new ManualOrigin(ManualSource.Search));

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var remaining = await WantlistIdsAsync();
		await Assert.That(remaining).DoesNotContain(ownedButGone);
		await Assert.That(remaining).Contains(addedByHand);
	}

	[Test]
	public async Task RunAsync_WhenTheProviderFailsOutright_LeavesTheWantlistAlone()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("broken-list", new Mirror(1));
		host.MdblistFeedReturnsStatus("broken-list", System.Net.HttpStatusCode.InternalServerError);
		var owned = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(await WantlistIdsAsync()).Contains(owned);
	}

	[Test]
	public async Task RunAsync_WhenTheListCannotBeFound_FailsAndRecordsWhyOnTheRun()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("missing-list", new Mirror(1));
		host.MdblistFeedReturnsStatus("missing-list", System.Net.HttpStatusCode.NotFound);

		var result = await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var failed = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunFailed>();
		var recorded = (await host.Resolve<IFeedRepository>().GetRunsAsync(feedId, take: 1)).Single();
		await Assert.That(failed.Reason).IsEqualTo("No list found with identifier 'missing-list'.");
		await Assert.That(recorded.SourceFailure).IsEqualTo(failed.Reason);
	}

	[Test]
	public async Task PlanAsync_ForAFeedWithItems_ReturnsAPlanAndChangesNothing()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("preview-list", new Mirror(1));
		host.MdblistFeedContains("preview-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();
		var owned = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));

		var result = await host.Resolve<IFeedRunPlanner>().PlanAsync(new SavedFeed(feedId));

		var plan = await Assert.That(result).IsOfUnionType<FeedPlanResult, MirrorPlan>();
		await Assert.That(plan!.Delta.Drops).Count().IsEqualTo(1);
		await Assert.That(await WantlistIdsAsync()).Contains(owned);
	}

	[Test]
	public async Task PlanAsync_WithoutAnMdblistKey_ReturnsAPlanThatSaysWhyTheSourceFailed()
	{
		var feedId = await SeedFeedAsync("keyless-list", new Mirror(1));

		var result = await host.Resolve<IFeedRunPlanner>().PlanAsync(new SavedFeed(feedId));

		var plan = await Assert.That(result).IsOfUnionType<FeedPlanResult, MirrorPlan>();
		await Assert.That(plan!.SourceFailure).IsEqualTo("No MDBList API key is configured. Add one in Settings.");
	}

	[Test]
	public async Task RunAsync_ForABlocklistedTitleTheFeedOwns_RemovesItLikeAnyOtherOnceItStopsQualifying()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("blocklist-list", new Mirror(1));
		host.MdblistFeedContains("blocklist-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();

		var blocklisted = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));
		await host.Resolve<IBlocklistRepository>().AddAsync(blocklisted);

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(await WantlistIdsAsync()).DoesNotContain(blocklisted);
	}

	[Test]
	public async Task RunAsync_ForABlocklistedTitleTheFeedOwns_KeepsItWhileItStillQualifies()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("blocklist-kept-list", new Mirror(1));
		host.MdblistFeedContains("blocklist-kept-list", Item("Inception", MediaType.Movie, Tmdb("27205")));

		var blocklisted = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));
		await host.Resolve<IBlocklistRepository>().AddAsync(blocklisted);

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(await WantlistIdsAsync()).Contains(blocklisted);
	}

	[Test]
	public async Task RunAsync_ForATitleAnotherFeedOwns_LeavesItAloneEvenWhenAbsentFromThisList()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var runningFeed = await SeedFeedAsync("running-list", new Mirror(1));
		var otherFeed = await SeedFeedAsync("other-list", new Mirror(1));
		host.MdblistFeedContains("running-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();

		var ownedByOther = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(otherFeed));

		var result = await Testee.RunAsync(runningFeed, new FeedRunTrigger(new Manual()));

		var completed = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(await WantlistIdsAsync()).Contains(ownedByOther);
		await Assert.That(completed!.Delta.Drops).DoesNotContain(ownedByOther);
	}

	[Test]
	public async Task RunAsync_WhenItAddsTitles_QueuesThemToPushInsteadOfLeavingTheSyncToRemoveThem()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("pushing-list", new Append());
		host.MdblistFeedContains("pushing-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var queued = await host.Resolve<IWantlistOutbox>().GetAsync();
		await Assert.That(queued.Where(op => op.Action == WantlistOperationAction.Add).Select(op => op.MediaId)).Contains(fightClub);
	}

	[Test]
	public async Task RunAsync_WhenMirrorRemovesATitle_QueuesTheRemovalSoTheSyncCannotPullItBack()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("dropping-list", new Mirror(1));
		host.MdblistFeedContains("dropping-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();
		var dropped = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var queued = await host.Resolve<IWantlistOutbox>().GetAsync();
		await Assert.That(queued.Where(op => op.Action == WantlistOperationAction.Remove).Select(op => op.MediaId)).Contains(dropped);
	}

	[Test]
	public async Task RunAsync_ForEachTitleItAdds_StampsTheRunThatAddedItSoProvenanceCanPointAtIt()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("stamping-list", new Append());
		host.MdblistFeedContains("stamping-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var recorded = (await host.Resolve<IFeedRepository>().GetRunsAsync(feedId, take: 1)).Single();
		var entry = (await host.Resolve<IWantlistRepository>().GetEntriesAsync()).Single(e => e.MediaId == fightClub);

		await Assert.That(entry.Provenance.Value is FeedOrigin origin && origin.Run.Id == recorded.Id).IsTrue();
	}

	[Test]
	public async Task RunAsync_ForAFeedThatNoLongerExists_FailsWithoutThrowing()
	{
		var result = await Testee.RunAsync(FeedId.From(Guid.NewGuid()), new FeedRunTrigger(new Manual()));

		await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunFailed>();
	}

	[Test]
	public async Task RunAsync_Always_RecordsTheRunSoItShowsInTheHistory()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("logged-list", new Append());
		host.MdblistFeedContains("logged-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var runs = await host.Resolve<IFeedRepository>().GetRunsAsync(feedId, take: 5);
		await Assert.That(runs).IsNotEmpty();
		await Assert.That(runs[0].Trigger).IsEqualTo(new FeedRunTrigger(new Manual()));
	}

	private static FeedItemSpec Item(string? title, MediaType type, params ProviderId[] ids) =>
		new(title, type, null, ids.ToDictionary(id => id.Provider, id => id.Value));

	private async Task<IReadOnlyList<MediaId>> WantlistIdsAsync() => await host.Resolve<IWantlistRepository>().GetIdsAsync();

	private async Task<FeedId> SeedFeedAsync(string identifier, FeedStrategy strategy)
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = strategy,
			Identifier = identifier,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
