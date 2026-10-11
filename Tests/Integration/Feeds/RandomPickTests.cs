using Contracts.Feed;
using Contracts.Wantlist;
using Core.Processing;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RandomPickTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string List = "sammlarr/random";

	private static readonly QueryLimit PickTwo = new() { Count = 2, Pick = LimitPick.Random };

	private static readonly MediaSpec[] Titles =
	[
		TestMedia.FightClub,
		TestMedia.Inception,
		TestMedia.Interstellar,
		new("Heat", MediaType.Movie, [Tmdb("949"), Plex("949")]),
		new("Memento", MediaType.Movie, [Tmdb("77"), Plex("77")]),
	];

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IFeedRunPlanner Planner => host.Resolve<IFeedRunPlanner>();

	[Test]
	public async Task PlanAsync_ForARandomPick_PlansWhatTheRunApplies()
	{
		await SeedListAsync(Titles);
		var feed = await CreateAsync(new Append());

		var plan = await Assert.That(await Planner.PlanAsync(new SavedFeed(feed))).IsOfUnionType<FeedPlanResult, AppendPlan>();
		await RunAsync(feed);

		await Assert.That(plan!.Delta.Adds).Count().IsEqualTo(2);
		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo(plan.Delta.Adds);
	}

	[Test]
	public async Task PlanAsync_ForANewFeedsDraft_PlansWhatItsFirstRunApplies()
	{
		await SeedListAsync(Titles);
		var draft = TestFeeds.Feed(Guid.NewGuid()) with { Identifier = List, Limit = Some.Of(PickTwo) };
		var load = new RawFeedLoad(
			await host.ResolveKeyed<IFeedProvider>(FeedProvider.Mdblist).GetFeedItemsAsync(List, await Planner.NeedsAsync(draft)).ToListAsync(),
			FeedLoadState.Finished
		);

		var plan = await Assert.That(await Planner.PlanAsync(new DraftFeed(draft, load))).IsOfUnionType<FeedPlanResult, AppendPlan>();
		var created = await host.Resolve<IFeedService>().CreatedAsync(Data(new Append()) with { Id = Some.Of(draft.Id) });
		await RunAsync(created);

		await Assert.That(created).IsEqualTo(draft.Id);
		await Assert.That(await WantlistIdsAsync()).IsEquivalentTo(plan!.Delta.Adds);
	}

	[Test]
	public async Task PlanAsync_ForARandomPick_KeepsHeldTitlesWhenOneLeavesTheList()
	{
		var seeded = await SeedListAsync(Titles);
		var feed = await CreateAsync(new Mirror(3));
		await RunAsync(feed);
		var held = await WantlistIdsAsync();
		var (leaving, staying) = (held[0], held[1]);
		host.MdblistFeedContains(List, [.. Titles.Where((_, index) => seeded[index] != leaving).Select(Listed)]);

		var plan = await Assert.That(await Planner.PlanAsync(new SavedFeed(feed))).IsOfUnionType<FeedPlanResult, MirrorPlan>();

		await Assert.That(Picked(plan!.Verdicts)).Count().IsEqualTo(2);
		await Assert.That(Picked(plan.Verdicts)).Contains(staying);
		await Assert.That(plan.Delta.Adds).Count().IsEqualTo(1);
		await Assert.That(plan.Delta.Drops).IsEmpty();
	}

	[Test]
	public async Task PlanAsync_ForARandomPick_KeepsHeldTitlesWhenBetterRankedOnesJoin()
	{
		var seeded = await SeedListAsync(Titles);
		var feed = FeedId.From(Guid.NewGuid());
		var lowest = Titles.Zip(seeded).OrderByDescending(title => ShuffleRank.Of(feed.Value, title.Second.Value)).Take(2).ToList();
		host.MdblistFeedContains(List, [.. lowest.Select(title => Listed(title.First))]);
		await RunAsync(await host.Resolve<IFeedService>().CreatedAsync(Data(new Append()) with { Id = Some.Of(feed) }));
		host.MdblistFeedContains(List, [.. Titles.Select(Listed)]);

		var plan = await Assert.That(await Planner.PlanAsync(new SavedFeed(feed))).IsOfUnionType<FeedPlanResult, AppendPlan>();

		await Assert.That(Picked(plan!.Verdicts)).IsEquivalentTo(lowest.Select(title => title.Second));
		await Assert.That(plan.Delta.Adds).IsEmpty();
	}

	[Test]
	public async Task PlanAsync_ForAnAppendFeedWithARandomPick_AddsNothingOnTheSecondRun()
	{
		await SeedListAsync(Titles);
		var feed = await CreateAsync(new Append());
		await RunAsync(feed);

		var plan = await Assert.That(await Planner.PlanAsync(new SavedFeed(feed))).IsOfUnionType<FeedPlanResult, AppendPlan>();

		await Assert.That(plan!.Delta.Adds).IsEmpty();
		await Assert.That(await WantlistIdsAsync()).Count().IsEqualTo(2);
	}

	private async Task<IReadOnlyList<MediaId>> SeedListAsync(IReadOnlyList<MediaSpec> titles)
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		host.MdblistFeedContains(List, [.. titles.Select(Listed)]);
		var seeded = new List<MediaId>();
		foreach (var title in titles)
		{
			seeded.Add(await host.Movie(title).SeedAsync());
		}

		return seeded;
	}

	private Task<FeedId> CreateAsync(FeedStrategy strategy) => host.Resolve<IFeedService>().CreatedAsync(Data(strategy));

	private static FeedCreateData Data(FeedStrategy strategy) =>
		new()
		{
			Name = "Random",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = strategy,
			Identifier = List,
			Limit = Some.Of(PickTwo),
			Schedule = new FeedRunTrigger(new Manual()),
		};

	private async Task RunAsync(FeedId feed)
	{
		await Assert.That(await host.Resolve<IFeedRunEngine>().RunAsync(feed, new FeedRunTrigger(new Manual()))).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
	}

	private async Task<IReadOnlyList<MediaId>> WantlistIdsAsync() => [.. (await host.Resolve<IWantlistRepository>().GetAllAsync()).Select(item => item.MediaId)];

	private static IEnumerable<MediaId> Picked(IEnumerable<QueryVerdict<MatchedMedia>> verdicts) =>
		verdicts.Where(verdict => verdict.Outcome is QueryOutcome.Qualified).Select(verdict => verdict.Item.Identity.Id);

	private static FeedItemSpec Listed(MediaSpec title) =>
		new(title.Title, title.Type, null, title.Ids.Where(id => id.Provider == MediaProvider.Tmdb).ToDictionary(id => id.Provider, id => id.Value));
}
