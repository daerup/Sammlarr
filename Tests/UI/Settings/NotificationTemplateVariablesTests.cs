using Domain.Notifications;
using UI.Features.Settings.Components;

namespace UI.Tests.Settings;

public class NotificationTemplateVariablesTests
{
	[Test]
	public async Task Feed_run_failed_shows_reason_and_feed_but_not_media()
	{
		var tokens = Tokens(NotificationEventType.FeedRunFailed);

		await Assert.That(tokens).Contains("{{ data.reason }}");
		await Assert.That(tokens).Contains("{{ data.feed.name }}");
		await Assert.That(tokens.Contains("{{ data.pulledAdds }}")).IsFalse();
		await Assert.That(tokens.Contains("{{ item.title }}")).IsFalse();
	}

	[Test]
	public async Task Watchlist_synced_shows_media_item_fields_with_descriptor()
	{
		var tokens = Tokens(NotificationEventType.WatchlistSynced);

		await Assert.That(tokens).Contains("{{ item.title }}");
		await Assert.That(tokens).Contains("{{ item.descriptor }}");
		await Assert.That(tokens.Contains("{{ data.pulledAdds }}")).IsFalse();
		await Assert.That(tokens.Contains("{{ data.reason }}")).IsFalse();
	}

	[Test]
	public async Task Top_level_variables_are_always_present()
	{
		await Assert.That(Tokens(NotificationEventType.FeedRunCompleted)).Contains("{{ title }}");
	}

	[Test]
	public async Task Multiple_events_show_the_union_of_their_variables()
	{
		var tokens = Tokens(NotificationEventType.FeedRunFailed, NotificationEventType.WatchlistSynced);

		await Assert.That(tokens).Contains("{{ data.reason }}");
		await Assert.That(tokens).Contains("{{ item.title }}");
	}

	private static HashSet<string> Tokens(params NotificationEventType[] eventTypes) =>
		NotificationTemplateVariables.GroupsFor(eventTypes).SelectMany(group => group.Variables).Select(variable => variable.Token).ToHashSet();
}
