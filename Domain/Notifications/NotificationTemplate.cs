namespace Domain.Notifications;

public sealed record NotificationTemplate(Optional<string> Body, Optional<string> Title, Optional<string> Message, Optional<int> Priority)
{
	public static NotificationTemplate None { get; } = new(new None(), new None(), new None(), new None());
}
