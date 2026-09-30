using System.ComponentModel;

namespace Domain.Notifications;

public enum NotificationEventType
{
	[Description("Wantlist sync completed")]
	WantlistSyncCompleted,

	[Description("Wantlist sync failed")]
	WantlistSyncFailed,

	[Description("Test notification")]
	TestPing,

	[Description("Feed run completed")]
	FeedRunCompleted,

	[Description("Feed run failed")]
	FeedRunFailed,

	[Description("Provider rate limited")]
	ProviderRateLimited,
}
