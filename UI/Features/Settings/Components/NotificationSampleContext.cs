using Contracts.Notifications;
using Domain.Notifications;
using Shared.Extensions;

namespace UI.Features.Settings.Components;

internal static class NotificationSampleContext
{
	private static readonly IReadOnlyList<NotificationMedia> SyncedMedia =
	[
		new("sample-1", "The Matrix", "Movie", 1999, "Added locally"),
		new("sample-2", "Dune", "Movie", 2021, "Added remotely"),
		new("sample-3", "Breaking Bad", "Show", 2008, "Removed locally"),
	];

	private static readonly NotificationFeed Feed = new("sample-feed", "My Feed", "Mdblist", "sample-list");

	public static NotificationContext For(NotificationEventType eventType)
	{
		var data = eventType switch
		{
			NotificationEventType.WatchlistSynced => new NotificationData { Media = SyncedMedia },
			NotificationEventType.FeedRunCompleted => new NotificationData
			{
				Feed = Feed,
				Added = 2,
				Removed = 1,
			},
			NotificationEventType.FeedRunFailed => new NotificationData { Feed = Feed, Reason = "Provider timed out" },
			NotificationEventType.TestPing => new NotificationData(),
		};
		return new NotificationContext(
			Guid.CreateVersion7(),
			eventType,
			DateTimeOffset.UtcNow,
			eventType.GetDescription(),
			"Sample notification for testing.",
			data
		);
	}
}
