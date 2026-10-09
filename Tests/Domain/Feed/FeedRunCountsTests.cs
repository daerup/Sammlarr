using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;

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

	[Test]
	public async Task From_WithStoredAndRememberedTitles_CountsKnownAndNotFoundEarlier()
	{
		QueryVerdict<MatchedMedia>[] verdicts = [Verdict(1, known: true), Verdict(2, known: true), Verdict(3, known: false)];
		UnmatchedMedia[] unmatched = [Unmatched(4, UnmatchedReason.NotFoundEarlier), Unmatched(5, UnmatchedReason.NotFound)];
		var plan = FeedPlans.Compute(
			new ClassifiedFeedLoad(verdicts, unmatched, FeedLoadState.Finished),
			new HashSet<MediaId>(),
			new Dictionary<MediaId, int>(),
			new Append()
		);

		var counts = FeedRunCounts.From(plan);

		await Assert.That((counts.Known, counts.NotFoundEarlier, counts.UnmatchedNotFound, counts.Total)).IsEqualTo((2, 1, 2, 5));
	}

	private static QueryVerdict<MatchedMedia> Verdict(int seed, bool known)
	{
		var media = new MatchedMedia(new MediaIdentity(MediaId.New(), [TmdbId.From($"matched-{seed}", MediaType.Movie)]), new MovieDetails()) { Known = known };
		return new QueryVerdict<MatchedMedia>(
			media,
			QueryOutcome.Qualified,
			new Enrollment
			{
				InWantlist = false,
				InBlocklist = false,
				Owned = false,
			},
			ConsumesSlot: true
		);
	}

	private static UnmatchedMedia Unmatched(int seed, UnmatchedReason reason)
	{
		return new([TmdbId.From($"unmatched-{seed}", MediaType.Movie)], new MovieDetails(), reason);
	}
}
