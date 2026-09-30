using Contracts.Feed;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunGraceTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private const int Threshold = 3;

	[Test]
	public async Task PlanAsync_ForAnOwnedItemMissingFromTheList_DropsItOnlyOnceGraceIsSpent()
	{
		var feedId = await SeedFeedAsync();
		var feed = (await fixture.Resolve<IFeedRepository>().GetFeedByIdAsync(feedId))!;
		var origin = await fixture.SeedFeedOriginAsync(feedId);

		var kept = await fixture.Movie(TestMedia.FightClub).OnWantlist(origin);
		var missing = await fixture.Movie(TestMedia.Inception).OnWantlist(origin);

		var planner = fixture.Resolve<IFeedRunPlanner>();
		var log = fixture.Resolve<IFeedRepository>();
		var load = new RawFeedLoad([Lookup(kept)], Completeness.Complete);

		var drops = new List<int>();

		for (var run = 1; run <= Threshold; run++)
		{
			var summary = await Assert.That(await planner.PlanAsync(new DraftFeed(feed, load))).IsOfUnionType<FeedPlanResult, MirrorPlan>();
			drops.Add(summary!.Delta.Drops.Count);

			await log.StartRunAsync(
				new FeedRun
				{
					Id = Guid.NewGuid(),
					FeedId = feedId,
					StartedAt = new DateTimeOffset(2026, 03, run, 4, 0, 0, TimeSpan.Zero),
					Duration = TimeSpan.FromSeconds(1),
					Strategy = feed.Strategy,
					Trigger = new FeedRunTrigger(new Manual()),
					Conclusiveness = summary!.Conclusiveness,
					Counts = FeedRunCounts.From(summary),
					Items = [new FeedRunItem { MediaId = kept, State = FeedRunItemState.Kept }],
				}
			);
		}

		await Assert.That(drops).IsEquivalentTo([0, 0, 1], CollectionOrdering.Matching);

		var final = await Assert.That(await planner.PlanAsync(new DraftFeed(feed, load))).IsOfUnionType<FeedPlanResult, MirrorPlan>();
		await Assert.That(final!.Delta.Drops).IsEquivalentTo([missing]);
		await Assert.That(final.MissedRuns[missing]).IsGreaterThanOrEqualTo(Threshold);
	}

	[Test]
	public async Task PlanAsync_WhenTheListOutageIsInconclusive_NeverBurnsGrace()
	{
		var feedId = await SeedFeedAsync();
		var feed = (await fixture.Resolve<IFeedRepository>().GetFeedByIdAsync(feedId))!;
		var origin = await fixture.SeedFeedOriginAsync(feedId);

		var owned = await fixture.Movie(TestMedia.FightClub).OnWantlist(origin);

		var planner = fixture.Resolve<IFeedRunPlanner>();
		var log = fixture.Resolve<IFeedRepository>();

		for (var run = 1; run <= Threshold + 2; run++)
		{
			var summary = await Assert
				.That(await planner.PlanAsync(new DraftFeed(feed, new RawFeedLoad([], Completeness.Complete))))
				.IsOfUnionType<FeedPlanResult, MirrorPlan>();

			await log.StartRunAsync(
				new FeedRun
				{
					Id = Guid.NewGuid(),
					FeedId = feedId,
					StartedAt = new DateTimeOffset(2026, 03, run, 4, 0, 0, TimeSpan.Zero),
					Duration = TimeSpan.FromSeconds(1),
					Strategy = feed.Strategy,
					Trigger = new FeedRunTrigger(new Manual()),
					Conclusiveness = summary!.Conclusiveness,
					Counts = FeedRunCounts.From(summary),
					Items = [],
				}
			);
		}

		var absences = await log.GetAbsencesAsync(feedId, new HashSet<MediaId> { owned }, Threshold);

		await Assert.That(absences).IsEmpty();
	}

	private static MediaLookup Lookup(MediaId id) =>
		new PartialMovie
		{
			Identity = new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			Title = "Title",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(90),
			Rating = RatingSummary.None,
		}.ToMatch();

	private async Task<FeedId> SeedFeedAsync()
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Mirror(Threshold)),
			Identifier = "hdlists/comedy",
		};

		fixture.Context.Feeds.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
