using System.Diagnostics.Metrics;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Ratings;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Harness;
using Microsoft.EntityFrameworkCore;
using Observability.Usage;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunApplyTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Fetched = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

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
	public async Task RunAsync_WhileItsCallsAreMeasured_SavesTheRunWithTheCallsItMade()
	{
		using var listener = MeasuringCalls();
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("measured-list", new Append());
		host.MdblistFeedContains("measured-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		using (UsageCauses.Mark(feedId))
		{
			await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		}

		var recorded = await LastRunAsync(feedId);
		await Assert.That(recorded.Calls.Where(call => call.Service == RemoteService.Mdblist).Sum(call => call.Calls)).IsGreaterThan(0);
	}

	[Test]
	public async Task RunAsync_ForATitleWithoutASinkId_CompletesAndAddsTheRest()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("switched-list", new Append());
		host.MdblistFeedContains("switched-list", Item("Fight Club", MediaType.Movie, Tmdb("550")), Item("Breaking Bad", MediaType.Show, Tmdb("1396")));
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		var breakingBad = await host.Show(TestMedia.BreakingBad).SeedAsync();
		host.PlexHasNoMetadataFor(TestMedia.BreakingBad);
		var feedRead = host.HoldMdblistFeed("switched-list");

		var running = Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		await feedRead.Arrived;
		await SwitchSinkAsync(SinkKind.Arr);
		feedRead.Release();

		await Assert.That(await running).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo([fightClub]);
		var recorded = await LastRunAsync(feedId);
		await Assert.That(recorded.Items.Where(item => item.State is FeedRunItemState.Unaddressable).Select(item => item.MediaId)).IsEquivalentTo([breakingBad]);
	}

	[Test]
	public async Task RunAsync_WhenTheApplyThrows_SavesTheRunAsStoppedWithItsCalls()
	{
		using var listener = MeasuringCalls();
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("rejected-list", new Append());
		host.MdblistFeedContains("rejected-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.Context.Database.ExecuteSqlRawAsync("""CREATE TRIGGER "RejectAdds" BEFORE INSERT ON "Wantlist" BEGIN SELECT RAISE(ABORT, 'locked'); END;""");

		using (UsageCauses.Mark(feedId))
		{
			await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		}

		var recorded = await LastRunAsync(feedId);
		await Assert.That(recorded.Conclusiveness).IsEqualTo(Conclusiveness.Stopped);
		await Assert.That(recorded.SourceFailure).IsSome();
		await Assert.That(recorded.Calls.Where(call => call.Service == RemoteService.Mdblist).Sum(call => call.Calls)).IsGreaterThan(0);
	}

	[Test]
	public async Task RunAsync_WhenCancelled_SavesTheRunAsEnded()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("cancelled-list", new Append());
		host.MdblistFeedContains("cancelled-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		var feedRead = host.HoldMdblistFeed("cancelled-list");
		using var cancellation = new CancellationTokenSource();

		var running = Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()), cancellation.Token);
		await feedRead.Arrived;
		await cancellation.CancelAsync();
		feedRead.Release();
		await running;

		var recorded = await LastRunAsync(feedId);
		await Assert.That(recorded.Conclusiveness).IsEqualTo(Conclusiveness.Stopped);
		await Assert.That(recorded.SourceFailure).IsSome().And.IsEqualTo(FeedRun.Cancelled);
	}

	[Test]
	public async Task RunAsync_WhileItRuns_ShowsPendingInRunHistory()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("pending-list", new Append());
		host.MdblistFeedContains("pending-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();
		var feedRead = host.HoldMdblistFeed("pending-list");

		var running = Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		await feedRead.Arrived;
		var during = await LastRunAsync(feedId);
		feedRead.Release();
		await running;

		await Assert.That(during.Conclusiveness).IsEqualTo(Conclusiveness.Pending);
		await Assert.That((await LastRunAsync(feedId)).Conclusiveness).IsEqualTo(Conclusiveness.Conclusive);
	}

	[Test]
	public async Task RunAsync_ForAMirrorFeedMissingAnOwnedTitle_CountsItsAbsence()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("absent-list", new Mirror(3));
		host.MdblistFeedContains("absent-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.Movie(TestMedia.FightClub).SeedAsync();
		var gone = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(await host.Resolve<IFeedRepository>().GetAbsencesAsync(feedId)).IsEquivalentTo(new Dictionary<MediaId, int> { [gone] = 1 });
	}

	[Test]
	public async Task RunAsync_ForAMirrorFeedWithACouldNotLookUpTitle_RemovesNothing()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("unresolved-list", new Mirror(1));
		host.MdblistFeedContains(
			"unresolved-list",
			Item("Fight Club", MediaType.Movie, Tmdb("550")),
			Item("Heat", MediaType.Movie, Tmdb("949")) with
			{
				Year = 1995,
			}
		);
		await host.Movie(TestMedia.FightClub).SeedAsync();
		var ownedButGone = await host.Movie(TestMedia.Inception).OnWantlist(await host.SeedFeedOriginAsync(feedId));
		await host.WithPlexAccount("plex-token");
		await host.RateLimitedAsync(RemoteService.Plex);

		var result = await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var completed = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(completed!.Conclusiveness).IsEqualTo(Conclusiveness.Unresolved);
		await Assert.That(await WantlistIdsAsync()).Contains(ownedButGone);
	}

	[Test]
	public async Task RunAsync_WithTmdbBlocked_SettlesTheTitlesThatDontNeedTmdb()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("tmdb-blocked-list", new Append());
		host.MdblistFeedContains(
			"tmdb-blocked-list",
			Item("Fight Club", MediaType.Movie, Tmdb("550")),
			Item("Heat", MediaType.Movie, Tmdb("949")) with
			{
				Year = 1995,
			}
		);
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.WithPlexAccount("plex-token");
		host.PlexHolds(TestMedia.FightClub);
		await host.PlexFindsNothing("Heat");
		await host.EnsureTmdbAccount();
		await host.RateLimitedAsync(RemoteService.Tmdb);

		var result = await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var completed = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(completed!.Conclusiveness).IsEqualTo(Conclusiveness.Unresolved);
		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo([fightClub]);
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
	public async Task PlanAsync_ForAListNamingOneTitleTwice_KeepsOneRowThatUsesOneSlotOfTheLimit()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("twice-list", new Append(), new QueryLimit { Count = 2 });
		host.MdblistFeedContains(
			"twice-list",
			Item("Fight Club", MediaType.Movie, Tmdb("550")),
			Item("Fight Club", MediaType.Movie, Tmdb("550")),
			Item("Inception", MediaType.Movie, Tmdb("27205"))
		);
		await host.Movie(TestMedia.FightClub).SeedWithDetailsAsync("Fight Club", Fetched);
		await host.Movie(TestMedia.Inception).SeedWithDetailsAsync("Inception", Fetched);

		var result = await host.Resolve<IFeedRunPlanner>().PlanAsync(new SavedFeed(feedId));

		var plan = await Assert.That(result).IsOfUnionType<FeedPlanResult, AppendPlan>();
		string?[] titles = ["Fight Club", "Inception"];
		await Assert.That(plan!.Verdicts.Select(verdict => verdict.Item.Details.Title)).IsEquivalentTo(titles);
		await Assert.That(plan.Verdicts.Select(verdict => verdict.Outcome)).IsEquivalentTo([QueryOutcome.Qualified, QueryOutcome.Qualified]);
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

	private async Task<FeedRun> LastRunAsync(FeedId feedId)
	{
		var runs = await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feedId) });
		return runs.Single();
	}

	private async Task SwitchSinkAsync(SinkKind sink)
	{
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync((await config.GetAsync()) with { Sink = sink });
	}

	private static MeterListener MeasuringCalls()
	{
		var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meters) =>
		{
			if (instrument.Name == "resilience.polly.pipeline.duration")
			{
				meters.EnableMeasurementEvents(instrument);
			}
		};
		listener.Start();
		return listener;
	}

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

	private async Task<FeedId> SeedFeedAsync(string identifier, FeedStrategy strategy, QueryLimit? limit = null)
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = strategy,
			Identifier = identifier,
			Limit = limit,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
