using Domain.Identity;
using Domain.Notifications.Events;
using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class NotificationEventCatalogTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

	[Test]
	public async Task Catalog_ForTheWatchlistSyncEvent_IncludesIt()
	{
		var catalog = NotificationEventCatalog.Subscribable.Select(option => option.EventType).ToHashSet();

		await Assert.That(catalog).Contains(new WatchlistSyncedEvent(User, Now, [], [], [], []).Type);
	}

	[Test]
	public async Task Catalog_ForGranularAndInternalEvents_ExcludesThem()
	{
		var catalog = NotificationEventCatalog.Subscribable.Select(option => option.EventType).ToHashSet();

		await Assert.That(catalog.Contains(new TestPingEvent(User, Now, Guid.NewGuid()).Type)).IsFalse();
	}

	[Test]
	public async Task Catalog_ForEveryOption_ProvidesALabel()
	{
		await Assert.That(NotificationEventCatalog.Subscribable).IsNotEmpty();
		await Assert.That(NotificationEventCatalog.Subscribable.All(option => !string.IsNullOrWhiteSpace(option.Label))).IsTrue();
	}
}
