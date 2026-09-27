namespace Domain.Notifications;

public sealed record NotificationTemplate(
	Optional<string> Body,
	Optional<string> Title,
	Optional<string> Message,
	Optional<NotificationLevel> Level,
	Optional<PushoverPriority> Priority
)
{
	public static NotificationTemplate None { get; } = new(new None(), new None(), new None(), new None(), new None());
}
