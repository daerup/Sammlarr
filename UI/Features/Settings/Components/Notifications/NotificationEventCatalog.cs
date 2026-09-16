using Domain.Notifications;

namespace UI.Features.Settings.Components.Notifications;

internal sealed record NotificationEventOption(NotificationEventType EventType, string Label);

internal static class NotificationEventCatalog
{
	private static readonly IReadOnlyList<NotificationEventType> NotSubscribable = [NotificationEventType.TestPing];

	public static readonly IReadOnlyList<NotificationEventOption> Subscribable = Enum.GetValues<NotificationEventType>()
		.Except(NotSubscribable)
		.Select(type => new NotificationEventOption(type, type.GetDescription()))
		.ToList();
}
