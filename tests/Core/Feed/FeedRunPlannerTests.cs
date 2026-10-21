using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Processing;
using Contracts.Ratings;
using Contracts.Wantlist;
using Core.Feed;
using Core.Processing;
using Domain.Blocklist;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TestInfrastructure;

namespace Core.Tests.Feed;

public class FeedRunPlannerTests
{
	private static readonly DraftFeed Draft = new(TestFeeds.Feed(Guid.NewGuid()), new RawFeedLoad([], FeedLoadState.Finished));

	[Test]
	public async Task PlanAsync_WhenCancelled_Throws()
	{
		var ratings = IRatingService.Mock();
		ratings.GetDefaultAsync(Any()).Returns(token => Task.FromCanceled<RatingStrategy>(token));
		using var leaving = new CancellationTokenSource();
		await leaving.CancelAsync();

		await Assert.That(async () => await Testee(ratings).PlanAsync(Draft, leaving.Token)).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task PlanAsync_WhenAnHttpTimeoutCancels_ReturnsAFailedPlan()
	{
		var ratings = IRatingService.Mock();
		ratings.GetDefaultAsync(Any()).Returns(_ => Task.FromException<RatingStrategy>(new TaskCanceledException("The request timed out.")));

		var plan = await Testee(ratings).PlanAsync(Draft, CancellationToken.None);

		await Assert.That(plan).IsOfUnionType<FeedPlanResult, FeedPlanFailed>();
	}

	[Test]
	public async Task PlanAsync_ForADraft_KeepsTheFailedRulesOfACulledTitle()
	{
		var fewEpisodes = QueryFilterFactory.Number(nameof(MediaQueryRow.Episodes), FilterPredicateOperator.LessThan, 200);
		var feed = TestFeeds.Feed(Guid.NewGuid()) with { Filter = Some.Of(QueryFilterFactory.Filter(QueryFilterFactory.Group(fewEpisodes))) };
		var onePiece = new MatchedMedia(
			new MediaIdentity(MediaId.New(), [TmdbId.From("37854", MediaType.Show)]),
			new ShowDetails { Title = "One Piece", Episodes = 1219 }
		);
		var ratings = IRatingService.Mock();
		ratings.GetDefaultAsync(Any()).Returns(new RatingStrategy(AveragedRating.Everything));
		ratings.GetAvailableAsync(Any()).Returns([RatingSource.Tmdb]);
		var wantlist = IWantlistRepository.Mock();
		wantlist.GetAllAsync(Any()).Returns([]);
		var blocklist = IBlocklistService.Mock();
		blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));

		var result = await Testee(ratings, wantlist, blocklist)
			.PlanAsync(new DraftFeed(feed, new RawFeedLoad([new Listed<MediaMatchResult>(0, onePiece)], FeedLoadState.Finished)), CancellationToken.None);

		var plan = await Assert.That(result).IsOfUnionType<FeedPlanResult, IFeedPlan>();
		var culled = await Assert.That(plan!.Verdicts.Single().Outcome).IsOfUnionType<QueryOutcome, Culled>();
		await Assert.That(culled.Checks.Failed).IsEquivalentTo([fewEpisodes]);
	}

	private static FeedRunPlanner Testee(Mock<IRatingService> ratings, Mock<IWantlistRepository>? wantlist = null, Mock<IBlocklistService>? blocklist = null)
	{
		return new FeedRunPlanner(
			(wantlist ?? IWantlistRepository.Mock()).Object,
			(blocklist ?? IBlocklistService.Mock()).Object,
			new QueryProcessor<MediaQueryRow>(),
			IFeedRepository.Mock().Object,
			new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
			ICurrentUser.Mock().Object,
			ratings.Object,
			new FakeTimeProvider(),
			NullLogger<FeedRunPlanner>.Instance
		);
	}
}
