using Domain.Feed;
using TestInfrastructure;

namespace Domain.Tests.Feed;

public class FeedSnapshotTests
{
	private static readonly Domain.Feed.Feed Comedy = TestFeeds.Feed(new Guid(1, 0, 0, new byte[8]), "Comedy");
	private static readonly Domain.Feed.Feed Horror = TestFeeds.Feed(new Guid(2, 0, 0, new byte[8]), "Horror");
	private static readonly FeedOverview ComedyOverview = FeedOverview.Of(Comedy, null, 3);

	private static readonly FeedSnapshot Testee = new([ComedyOverview]);

	[Test]
	public async Task Find_ForAKnownFeed_ReturnsItsOverview() => await Assert.That(Testee.Find(Comedy.Id)).IsSome().And.IsEqualTo(ComedyOverview);

	[Test]
	public async Task Find_ForAnUnknownFeed_ReturnsNone() => await Assert.That(Testee.Find(Horror.Id)).IsNone();
}
