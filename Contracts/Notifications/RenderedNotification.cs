namespace Contracts.Notifications;

public sealed record RenderedNotification(Optional<string> Title, Optional<string> Message, Optional<string> Body)
{
	public static RenderedNotification Empty { get; } = new(new None(), new None(), new None());
}
