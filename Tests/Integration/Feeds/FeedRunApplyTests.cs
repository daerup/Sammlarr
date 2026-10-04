using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Ratings;
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
using Microsoft.EntityFrameworkCore;
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
		var recorded = (await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feedId) })).Single();
		await Assert.That(failed.Reason).IsEqualTo("No list found with identifier 'missing-list'.");
		await Assert.That(recorded.SourceFailure).IsSome().And.IsEqualTo(failed.Reason);
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
		await Assert.That(plan!.SourceFailure).IsSome().And.IsEqualTo("No MDBList API key is configured. Add one in Settings.");
	}

	[Test]
	public async Task RunAsync_ForABlocklistedTitleTheFeedOwns_RemovesItLikeAnyOtherOnceItStopsQualifying()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("blocklist-list", new Mirror(1));
		host.MdblistFeedContains("blocklist-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();

		var blocklisted = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));
		await host.Resolve<IBlocklistRepository>().AddAsync([blocklisted]);

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
		await host.Resolve<IBlocklistRepository>().AddAsync([blocklisted]);

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

		var queued = await host.Resolve<IWantlistOutbox>().GetAllAsync();
		await Assert.That(queued.Where(op => op.Action == WantlistOperationAction.Add).Select(op => op.MediaId)).Contains(fightClub);
	}

	[Test]
	public async Task RunAsync_ForAMirrorFeed_AnnouncesWhatItAddedAndDropped()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("announced-list", new Mirror(1));
		host.MdblistFeedContains("announced-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		var dropped = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));
		var heard = host.Heard();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var changes = heard.OfType<WantlistChangedEvent>().ToList();
		await Assert.That(changes.SelectMany(change => change.Added)).IsEquivalentTo([fightClub]);
		await Assert.That(changes.SelectMany(change => change.Removed)).IsEquivalentTo([dropped]);
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

		var queued = await host.Resolve<IWantlistOutbox>().GetAllAsync();
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

		var recorded = (await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feedId) })).Single();
		var item = (await host.Resolve<IWantlistRepository>().GetAllAsync()).Single(added => added.MediaId == fightClub);

		await Assert
			.That(item.Provenance)
			.IsOfUnionType<WantlistItemProvenance, FeedOrigin>()
			.And.Multiple(async origin => await Assert.That(origin.Run.Id).IsEqualTo(recorded.Id));
	}

	[Test]
	public async Task RunAsync_ForAFeedWithAStoredFilterSortAndLimit_AddsOnlyTheTopRatedTitleTheFilterLetsThrough()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("top-rated-list", new Append());
		await host.Context.Database.ExecuteSqlAsync(
			$$"""
			UPDATE "Feeds" SET
				"Filter" = '{"Groups":[{"Predicates":[{"Field":"Rating","Operator":"Between","ValueKind":"Number","Value":"8","ValueEnd":"9"}],"Groups":[],"Operator":"And"}],"Operator":"And"}',
				"Sort" = '{"Property":"Rating","Direction":"Descending"}',
				"Limit" = '{"Count":1,"Wantlisted":"Consumed","Blocklisted":"Consumed"}'
			WHERE "Id" = {{feedId.Value}}
			"""
		);
		host.MdblistFeedContains(
			"top-rated-list",
			Item("Fight Club", MediaType.Movie, Tmdb("550")),
			Item("Inception", MediaType.Movie, Tmdb("27205")),
			Item("Interstellar", MediaType.Movie, Tmdb("157336"))
		);
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 1000).SeedAsync();
		var inception = await host.Movie(TestMedia.Inception).ServedByPlex().ServedByTmdb(rating: 8.8, votes: 1000).SeedAsync();
		await host.Movie(TestMedia.Interstellar).ServedByPlex().ServedByTmdb(rating: 9.5, votes: 1000).SeedAsync();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo([inception]);
	}

	[Test]
	public async Task RunAsync_ForAFeedOnFirstAvailableWithImdbFirst_FiltersOnTheImdbRatingWhereThereIsOne()
	{
		var rated = await SeedRatedFeedAsync("ordered:Imdb");

		await Testee.RunAsync(rated.Feed, new FeedRunTrigger(new Manual()));

		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo([rated.Inception]);
	}

	[Test]
	public async Task RunAsync_ForAFeedWithoutItsOwnRatingStrategy_FollowsItsOwnersDefault()
	{
		var rated = await SeedRatedFeedAsync(null);
		await host.Resolve<IRatingService>().StoreDefaultAsync(new RatingStrategy(new OrderedRating([RatingSource.Imdb])));

		await Testee.RunAsync(rated.Feed, new FeedRunTrigger(new Manual()));

		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo([rated.Inception]);
	}

	[Test]
	public async Task RunAsync_ForAFeedOnAverage_FiltersOnTheMeanOfEveryRating()
	{
		var rated = await SeedRatedFeedAsync("averaged");

		await Testee.RunAsync(rated.Feed, new FeedRunTrigger(new Manual()));

		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo([rated.FightClub, rated.Inception]);
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

		var runs = await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(5) with { Feed = Some.Of(feedId) });
		await Assert.That(runs).IsNotEmpty();
		await Assert.That(runs[0].Trigger).IsEqualTo(new FeedRunTrigger(new Manual()));
	}

	private static FeedItemSpec Item(string? title, MediaType type, params ProviderId[] ids) =>
		new(title, type, null, ids.ToDictionary(id => id.Provider, id => id.Value));

	private async Task<IReadOnlyList<MediaId>> WantlistIdsAsync() => [.. (await host.Resolve<IWantlistRepository>().GetAllAsync()).Select(item => item.MediaId)];

	private async Task<(FeedId Feed, MediaId FightClub, MediaId Inception)> SeedRatedFeedAsync(string? ratingStrategy)
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("rated-list", new Append());
		await host.Context.Database.ExecuteSqlAsync(
			$$"""
			UPDATE "Feeds" SET
				"Filter" = '{"Groups":[{"Predicates":[{"Field":"Rating","Operator":"GreaterOrEqual","ValueKind":"Number","Value":"7"}],"Groups":[],"Operator":"And"}],"Operator":"And"}',
				"RatingStrategy" = {{ratingStrategy}}
			WHERE "Id" = {{feedId.Value}}
			"""
		);
		host.MdblistFeedContains(
			"rated-list",
			Item("Fight Club", MediaType.Movie, Tmdb("550")),
			Item("Inception", MediaType.Movie, Tmdb("27205")),
			Item("Interstellar", MediaType.Movie, Tmdb("157336"))
		);
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex(imdbRating: 6.5, imdbVotes: 900).ServedByTmdb(rating: 8.4, votes: 1000).SeedAsync();
		var inception = await host.Movie(TestMedia.Inception)
			.ServedByPlex(imdbRating: 8.8, imdbVotes: 2_700_000)
			.ServedByTmdb(rating: 6.0, votes: 1000)
			.SeedAsync();
		await host.Movie(TestMedia.Interstellar).ServedByPlex().ServedByTmdb(rating: 6.5, votes: 1000).SeedAsync();
		return (feedId, fightClub, inception);
	}

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
