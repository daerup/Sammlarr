using Core.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;

namespace Core.Tests.Feed;

public class FeedRunNotificationTests
{
	private static readonly UserId Owner = UserId.From(Guid.NewGuid());
	private static readonly FeedId Id = FeedId.From(Guid.NewGuid());
	private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task For_ACompletedRun_CarriesTheAppliedCounts()
	{
		var notification = FeedRunNotification.For(Owner, Now, Id, Completed(added: 2, removed: 1));

		var completed = (FeedRunCompletedNotification)notification!;
		await Assert.That(completed.FeedId).IsEqualTo(Id);
		await Assert.That(completed.UserId).IsEqualTo(Owner);
		await Assert.That(completed.Added).IsEqualTo(2);
		await Assert.That(completed.Removed).IsEqualTo(1);
	}

	[Test]
	public async Task For_AFailedRun_CarriesWhyItCouldNotFinish()
	{
		var notification = FeedRunNotification.For(Owner, Now, Id, new FeedRunFailed("the provider timed out"));

		await Assert.That(((FeedRunFailedNotification)notification!).Reason).IsEqualTo("The run could not finish - the provider timed out");
	}

	[Test]
	public async Task For_ARunThatWasAlreadyGoing_IsNoNewsSoNobodyIsTold()
	{
		var notification = FeedRunNotification.For(Owner, Now, Id, new FeedRunAlreadyRunning());

		await Assert.That(notification).IsNull();
	}

	private static FeedRunCompleted Completed(int added, int removed) => new(new FeedDelta([.. Ids(added)], [], [.. Ids(removed)]), Conclusiveness.Conclusive);

	private static IEnumerable<MediaId> Ids(int count) => Enumerable.Range(0, count).Select(_ => MediaId.From(Guid.NewGuid()));
}
