using Domain.Announcing;

namespace Domain.Tests.Announcing;

public class AnnouncingMarkersTests
{
	[Test]
	public async Task Markers_AreDisjoint_SoAnAnnouncerCannotAcceptBothKindsInOneMethod()
	{
		await Assert.That(typeof(INotification).IsAssignableTo(typeof(IEvent))).IsFalse();
		await Assert.That(typeof(IEvent).IsAssignableTo(typeof(INotification))).IsFalse();
	}
}
