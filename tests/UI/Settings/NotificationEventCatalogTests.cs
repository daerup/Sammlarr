using Domain.Identity;
using Domain.Notifications;
using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class NotificationEventCatalogTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

	[Test]
	public async Task Catalog_HoldsEveryEventButTheTestPing()
	{
		var catalog = NotificationEventCatalog.Subscribable.Select(option => option.EventType).ToHashSet();

		await Assert.That(catalog).Contains(new WantlistSyncCompletedNotification(User, Now, [], [], [], [], []).Type);
		await Assert.That(catalog.Contains(new TestPingNotification(User, Now, Guid.NewGuid()).Type)).IsFalse();
	}

	[Test]
	public async Task Summarise_ForASingleEvent_NamesItRatherThanCountingIt()
	{
		var summary = NotificationEventCatalog.Summarise([NotificationEventType.FeedRunFailed]);

		await Assert.That(summary).IsEqualTo("Feed run failed");
	}

	[Test]
	public async Task Summarise_ForSeveralEvents_CountsThemSoTheRowStaysOneLine()
	{
		var summary = NotificationEventCatalog.Summarise([
			NotificationEventType.FeedRunFailed,
			NotificationEventType.FeedRunCompleted,
			NotificationEventType.WantlistSyncFailed,
		]);

		await Assert.That(summary).IsEqualTo("3 events");
	}

	[Test]
	public async Task Spell_ListsEveryEvent_SoTheTooltipStillShowsWhatTheSummaryHides()
	{
		var spelled = NotificationEventCatalog.Spell([NotificationEventType.FeedRunFailed, NotificationEventType.WantlistSyncCompleted]);

		await Assert.That(spelled).IsEqualTo("Feed run failed, Wantlist sync completed");
	}
}
