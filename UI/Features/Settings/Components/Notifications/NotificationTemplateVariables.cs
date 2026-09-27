using Domain.Notifications;

namespace UI.Features.Settings.Components.Notifications;

internal sealed record TemplateVariable(string Token, string Description);

internal sealed record TemplateVariableGroup(string Title, IReadOnlyList<TemplateVariable> Variables);

internal static class NotificationTemplateVariables
{
	private static readonly TemplateVariableGroup Notification = new(
		"Notification",
		[
			new("{{ title }}", "Headline text"),
			new("{{ summary }}", "Short summary line"),
			new("{{ type }}", "Event type"),
			new("{{ defaultLevel }}", "The notification's own level: Info, Success, Warning or Error"),
			new("{{ id }}", "Unique event id"),
			new("{{ occurredAt }}", "When it happened"),
		]
	);

	private static readonly TemplateVariableGroup MediaItem = new(
		"Media item",
		[
			new("{{ item.title }}", "Media title"),
			new("{{ item.year }}", "Release year"),
			new("{{ item.type }}", "Movie or Show"),
			new("{{ item.descriptor }}", "What happened to it"),
			new("{{ item.id }}", "Internal id"),
		]
	);

	private static readonly TemplateVariable FeedName = new("{{ data.feed.name }}", "Feed name");
	private static readonly TemplateVariable FeedProvider = new("{{ data.feed.provider }}", "Feed provider");
	private static readonly TemplateVariable FeedIdentifier = new("{{ data.feed.identifier }}", "Feed list identifier");
	private static readonly TemplateVariable Added = new("{{ data.added }}", "Count added");
	private static readonly TemplateVariable Removed = new("{{ data.removed }}", "Count removed");
	private static readonly TemplateVariable Reason = new("{{ data.reason }}", "Failure reason");
	private static readonly TemplateVariable Provider = new("{{ data.provider }}", "Provider that rate limited us");
	private static readonly TemplateVariable RetryAfter = new("{{ data.retryAfter }}", "Time until the retry");

	private static readonly Dictionary<NotificationEventType, TemplateVariable[]> DataByEvent = new()
	{
		[NotificationEventType.FeedRunCompleted] = [FeedName, FeedProvider, FeedIdentifier, Added, Removed],
		[NotificationEventType.FeedRunFailed] = [FeedName, FeedProvider, FeedIdentifier, Reason],
		[NotificationEventType.WatchlistSyncFailed] = [Reason],
		[NotificationEventType.ProviderRateLimited] = [Provider, RetryAfter],
	};

	private static readonly HashSet<NotificationEventType> HasMediaLists = [NotificationEventType.WatchlistSyncCompleted];

	public static IReadOnlyList<TemplateVariableGroup> GroupsFor(IReadOnlyCollection<NotificationEventType> eventTypes)
	{
		var groups = new List<TemplateVariableGroup> { Notification };

		var data = eventTypes.SelectMany(type => DataByEvent.GetValueOrDefault(type, [])).Distinct().ToList();
		if (data.Count > 0)
		{
			groups.Add(new TemplateVariableGroup("Data", data));
		}

		if (eventTypes.Any(HasMediaLists.Contains))
		{
			groups.Add(MediaItem);
		}

		return groups;
	}
}
