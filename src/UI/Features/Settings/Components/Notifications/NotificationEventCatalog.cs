using Domain.Notifications;
using Humanizer;

namespace UI.Features.Settings.Components.Notifications;

internal sealed record NotificationEventOption(NotificationEventType EventType, string Label);

internal static class NotificationEventCatalog
{
	private static readonly IReadOnlyList<NotificationEventType> NotSubscribable = [NotificationEventType.TestPing];

	public static readonly IReadOnlyList<NotificationEventOption> Subscribable = Enum.GetValues<NotificationEventType>()
		.Except(NotSubscribable)
		.Select(type => new NotificationEventOption(type, type.Humanize()))
		.ToList();

	public static string Summarise(IReadOnlyList<NotificationEventType> eventTypes) =>
		eventTypes.Count == 1 ? eventTypes[0].Humanize() : $"{eventTypes.Count} events";

	public static string Spell(IReadOnlyList<NotificationEventType> eventTypes) => string.Join(", ", eventTypes.Select(eventType => eventType.Humanize()));
}
