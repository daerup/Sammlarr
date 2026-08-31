using Domain.Identity;
using Domain.Notifications.Events;
using UI.Features.Settings.Components;

namespace UI.Tests.Settings;

public class NotificationEventCatalogTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

	[Test]
	public async Task Catalog_covers_the_watchlist_event_types()
	{
		var catalog = NotificationEventCatalog.Subscribable.Select(option => option.EventType).ToHashSet();

		await Assert.That(catalog).Contains(new WatchlistSyncedEvent(User, Now, [], [], [], []).Type);
		await Assert.That(catalog).Contains(new WatchlistItemsAddedEvent(User, Now, []).Type);
		await Assert.That(catalog).Contains(new WatchlistItemsRemovedEvent(User, Now, []).Type);
	}

	[Test]
	public async Task Catalog_excludes_the_test_ping_event()
	{
		var catalog = NotificationEventCatalog.Subscribable.Select(option => option.EventType).ToHashSet();

		await Assert.That(catalog.Contains(new TestPingEvent(User, Now, Guid.NewGuid()).Type)).IsFalse();
	}

	[Test]
	public async Task Catalog_options_all_have_labels()
	{
		await Assert.That(NotificationEventCatalog.Subscribable).IsNotEmpty();
		await Assert.That(NotificationEventCatalog.Subscribable.All(option => !string.IsNullOrWhiteSpace(option.Label))).IsTrue();
	}
}
