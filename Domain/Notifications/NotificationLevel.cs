namespace Domain.Notifications;

public enum NotificationLevel
{
	Info,
	Warning,
	Error,
}

public static class NotificationLevels
{
	extension(NotificationLevel)
	{
		public static NotificationLevel For(NotificationEventType type) =>
			type switch
			{
				NotificationEventType.FeedRunFailed => NotificationLevel.Error,
				NotificationEventType.ProviderRateLimited => NotificationLevel.Warning,
				NotificationEventType.WatchlistSynced => NotificationLevel.Info,
				NotificationEventType.FeedRunCompleted => NotificationLevel.Info,
				NotificationEventType.TestPing => NotificationLevel.Info,
			};
	}
}
