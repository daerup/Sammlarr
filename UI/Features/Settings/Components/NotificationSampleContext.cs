using Contracts.Notifications;

namespace UI.Features.Settings.Components;

internal static class NotificationSampleContext
{
	private static readonly IReadOnlyList<NotificationMedia> Media =
	[
		new("sample-1", "The Matrix", "Movie", 1999),
		new("sample-2", "Breaking Bad", "Show", 2008),
	];

	private static readonly IReadOnlyList<NotificationMedia> SyncedMedia =
	[
		new("sample-1", "The Matrix", "Movie", 1999, "Added locally"),
		new("sample-2", "Dune", "Movie", 2021, "Added remotely"),
		new("sample-3", "Breaking Bad", "Show", 2008, "Removed locally"),
	];

	private static readonly NotificationFeed Feed = new("sample-feed", "My Feed", "Mdblist", "sample-list");

	public static NotificationContext For(string eventType)
	{
		var data = eventType switch
		{
			"watchlist.synced" => new NotificationData { Media = SyncedMedia },
			"watchlist.items.added" => new NotificationData { Media = Media },
			"watchlist.items.removed" => new NotificationData { Media = Media },
			"feed.run.completed" => new NotificationData
			{
				Feed = Feed,
				Added = 2,
				Removed = 1,
			},
			"feed.run.failed" => new NotificationData { Feed = Feed, Reason = "Provider timed out" },
			_ => new NotificationData(),
		};
		return new NotificationContext(
			Guid.CreateVersion7(),
			eventType,
			DateTimeOffset.UtcNow,
			NotificationEventCatalog.LabelFor(eventType),
			"Sample notification for testing.",
			data
		);
	}
}
