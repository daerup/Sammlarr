using Domain.Announcing;
using Domain.Notifications;
using Domain.Wantlist.Outbox;

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
	public async Task INotification_IsANotification_AndNeverAnEvent()
	{
		await Assert.That(typeof(INotification).IsAssignableTo(typeof(INotification))).IsTrue();
		await Assert.That(typeof(INotification).IsAssignableTo(typeof(IEvent))).IsFalse();
	}

	[Test]
	public async Task WantlistOutboxEvent_IsAnEvent_AndNeverANotification()
	{
		await Assert.That(typeof(WantlistOutboxEvent).IsAssignableTo(typeof(IEvent))).IsTrue();
		await Assert.That(typeof(WantlistOutboxEvent).IsAssignableTo(typeof(INotification))).IsFalse();
	}
}
