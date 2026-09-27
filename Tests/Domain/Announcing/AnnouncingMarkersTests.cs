using Domain.Announcing;
using Domain.Notifications.Events;
using Domain.Watchlist.Outbox;

namespace Domain.Tests.Announcing;

public class AnnouncingMarkersTests
{
	[Test]
	public async Task Markers_AreDisjoint_SoAnAnnouncerCannotAcceptBothKindsInOneMethod()
	{
		await Assert.That(typeof(INotification).IsAssignableTo(typeof(IEvent))).IsFalse();
		await Assert.That(typeof(IEvent).IsAssignableTo(typeof(INotification))).IsFalse();
	}

	[Test]
	public async Task BaseNotification_IsANotification_AndNeverAnEvent()
	{
		await Assert.That(typeof(BaseNotification).IsAssignableTo(typeof(INotification))).IsTrue();
		await Assert.That(typeof(BaseNotification).IsAssignableTo(typeof(IEvent))).IsFalse();
	}

	[Test]
	public async Task WatchlistOutboxEvent_IsAnEvent_AndNeverANotification()
	{
		await Assert.That(typeof(WatchlistOutboxEvent).IsAssignableTo(typeof(IEvent))).IsTrue();
		await Assert.That(typeof(WatchlistOutboxEvent).IsAssignableTo(typeof(INotification))).IsFalse();
	}
}
