using Core.Wantlist.Sync;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using TestInfrastructure;

namespace Core.Tests.Wantlist;

public class SyncNotificationTests
{
	private static readonly UserId Owner = UserId.From(Guid.NewGuid());
	private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task For_ASyncThatMovedNothing_IsNoNewsSoNobodyIsTold()
	{
		await Assert.That(SyncNotification.For(Owner, Now, [], [], [])).IsNone();
	}

	[Test]
	public async Task For_ASyncThatPulledATitle_CarriesIt()
	{
		var pulled = MediaId.New();

		var notification = SyncNotification.For(Owner, Now, [pulled], [], []);

		var completed = (WantlistSyncCompletedNotification)(await Assert.That(notification).IsSome())!;
		await Assert.That(completed.UserId).IsEqualTo(Owner);
		await Assert.That(completed.PulledAdds).IsEquivalentTo([pulled]);
	}
}
