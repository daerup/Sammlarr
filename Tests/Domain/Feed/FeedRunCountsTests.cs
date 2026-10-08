using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;

namespace Domain.Tests.Feed;

public class FeedRunCountsTests
{
	[Test]
	public async Task From_APlanWithUnmatchedTitles_SplitsThemByWhyTheyStayedUnmatched()
	{
		UnmatchedMedia[] unmatched = [Unmatched(1, UnmatchedReason.NotFound), Unmatched(2, UnmatchedReason.NotFound), Unmatched(3, UnmatchedReason.CouldNotLookUp)];
		var plan = FeedPlans.Compute(
			new ClassifiedFeedLoad([], unmatched, FeedLoadState.Finished),
			new HashSet<MediaId>(),
			new Dictionary<MediaId, int>(),
			new Append()
		);

		var counts = FeedRunCounts.From(plan);

		await Assert.That((counts.UnmatchedNotFound, counts.UnmatchedCouldNotLookUp, counts.Unmatched, counts.Total)).IsEqualTo((2, 1, 3, 3));
	}

	private static UnmatchedMedia Unmatched(int seed, UnmatchedReason reason)
	{
		return new([TmdbId.From($"unmatched-{seed}", MediaType.Movie)], new MovieDetails(), reason);
	}
}
