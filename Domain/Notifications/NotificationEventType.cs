using System.ComponentModel;

namespace Domain.Notifications;

public enum NotificationEventType
{
	[Description("Watchlist synced")]
	WatchlistSynced,

	[Description("Test notification")]
	TestPing,

	[Description("Feed run completed")]
	FeedRunCompleted,

	[Description("Feed run failed")]
	FeedRunFailed,
}
