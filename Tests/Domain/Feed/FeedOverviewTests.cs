using Domain.Feed;
using Domain.Feed.Run;
using TestInfrastructure;

namespace Domain.Tests.Feed;

public class FeedOverviewTests
{
	private static readonly Domain.Feed.Feed Comedy = TestFeeds.Feed(new Guid(1, 0, 0, new byte[8]), "Comedy");

	[Test]
	public async Task Of_ForAFeedThatNeverRan_HasNoLastRunAndAnEmptyHistory()
	{
		var overview = FeedOverview.Of(Comedy, null, 0);

		await Assert.That(overview.LastRun).IsNone();
		await Assert.That(overview.History).IsEqualTo(FeedRunHistory.None);
	}

	[Test]
	public async Task Of_ForAFeedWithRuns_CarriesItsLastRunAndHistory()
	{
		var run = TestFeeds.Run(Comedy, Guid.NewGuid());
		var history = FeedRunHistory.None with { TotalRuns = 4 };

		var overview = FeedOverview.Of(Comedy, new FeedRunTotals(run, history), 0);

		await Assert.That(overview.LastRun).IsSome().And.IsEqualTo(run);
		await Assert.That(overview.History).IsEqualTo(history);
	}
}
