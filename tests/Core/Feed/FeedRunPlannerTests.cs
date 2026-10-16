using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Processing;
using Contracts.Ratings;
using Contracts.Wantlist;
using Core.Feed;
using Core.Processing;
using Domain.Feed.Planning;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

	private static FeedRunPlanner Testee(Mock<IRatingService> ratings)
	{
		return new FeedRunPlanner(
			IWantlistRepository.Mock().Object,
			IBlocklistService.Mock().Object,
			new QueryProcessor<MediaQueryRow>(),
			IFeedRepository.Mock().Object,
			new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
			ICurrentUser.Mock().Object,
			ratings.Object,
			NullLogger<FeedRunPlanner>.Instance
		);
	}
}
