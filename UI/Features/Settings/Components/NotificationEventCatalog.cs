namespace UI.Features.Settings.Components;

internal sealed record NotificationEventOption(string EventType, string Label);

internal static class NotificationEventCatalog
{
	public static readonly IReadOnlyList<NotificationEventOption> Subscribable =
	[
		new("watchlist.synced", "Watchlist synced"),
		new("watchlist.items.added", "Items added to watchlist"),
		new("watchlist.items.removed", "Items removed from watchlist"),
		new("feed.run.completed", "Feed run completed"),
		new("feed.run.failed", "Feed run failed"),
	];

	public static string LabelFor(string eventType) => Subscribable.FirstOrDefault(option => option.EventType == eventType)?.Label ?? eventType;
}
