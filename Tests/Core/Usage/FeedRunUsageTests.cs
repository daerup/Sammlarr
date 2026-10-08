using Core.Usage;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;

namespace Core.Tests.Usage;

public class FeedRunUsageTests
{
	private static readonly FeedId First = FeedId.From(Guid.NewGuid());

	private static readonly FeedId Second = FeedId.From(Guid.NewGuid());

	[Test]
	public async Task End_AfterCreditsToTwoFeeds_HandsEachRunOnlyItsOwnCalls()
	{
		var testee = new FeedRunUsage();
		testee.Start(First);
		testee.Start(Second);

		testee.Credit(First, RemoteService.Tmdb, UsageOutcome.Answered, 40);
		testee.Credit(Second, RemoteService.Plex, UsageOutcome.AnsweredAfter429, 3);
		testee.Credit(First, RemoteService.Tmdb, UsageOutcome.Answered, 2);

		await Assert.That(testee.End(First).Calls).IsEquivalentTo([new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, 42)]);
		await Assert.That(testee.End(Second).Calls).IsEquivalentTo([new FeedRunCalls(RemoteService.Plex, UsageOutcome.AnsweredAfter429, 3)]);
	}

	[Test]
	public async Task End_AfterTimedAttempts_CountsEachServicesAttemptsPerRange()
	{
		var testee = new FeedRunUsage();
		testee.Start(First);
		testee.Start(Second);

		testee.Time(First, RemoteService.Tmdb, 100);
		testee.Time(First, RemoteService.Tmdb, 100);
		testee.Time(First, RemoteService.Plex, 500);
		testee.Time(Second, RemoteService.Tmdb, 100);

		await Assert.That(testee.End(First).Times).IsEquivalentTo([new FeedRunTimes(RemoteService.Tmdb, 100, 2), new FeedRunTimes(RemoteService.Plex, 500, 1)]);
	}

	[Test]
	public async Task Credit_ForAFeedThatIsNotRunning_IsDropped()
	{
		var testee = new FeedRunUsage();

		testee.Credit(First, RemoteService.Tmdb, UsageOutcome.Answered, 5);
		testee.Time(First, RemoteService.Tmdb, 100);
		testee.Start(First);

		var cost = testee.End(First);
		await Assert.That(cost.Calls).IsEmpty();
		await Assert.That(cost.Times).IsEmpty();
		await Assert.That(testee.End(First).Calls).IsEmpty();
	}
}
