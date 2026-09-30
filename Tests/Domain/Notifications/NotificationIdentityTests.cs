using Domain.Announcing;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;

namespace Domain.Tests.Notifications;

public class NotificationIdentityTests
{
	private static readonly UserId Owner = UserId.From(Guid.NewGuid());
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

	public static IEnumerable<Func<INotification>> AllKinds() =>
		[
			() => new WantlistSyncCompletedNotification(Owner, Now, [], [], [], [], []),
			() => new WantlistSyncFailedNotification(Owner, Now, "nope"),
			() => new TestPingNotification(Owner, Now, Guid.NewGuid()),
			() => new FeedRunCompletedNotification(Owner, Now, FeedId.From(Guid.NewGuid()), 1, 0),
			() => new FeedRunFailedNotification(Owner, Now, FeedId.From(Guid.NewGuid()), "nope"),
			() => new ProviderRateLimitedNotification(Owner, Now, MediaProvider.Tmdb, TimeSpan.FromSeconds(1)),
		];

	[Test]
	[MethodDataSource(nameof(AllKinds))]
	public async Task Id_IsATimeOrderedVersion7Guid_SoDeliveryRecordsSortByWhenTheyHappened(INotification notification)
	{
		await Assert.That(notification.Id.Version).IsEqualTo(7);
	}

	[Test]
	public async Task Id_IsFreshPerInstance_SoTwoNotificationsNeverShareADeliveryKey()
	{
		foreach (var build in AllKinds())
		{
			await Assert.That(build().Id).IsNotEqualTo(build().Id);
		}
	}
}
