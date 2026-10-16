using Contracts.Feed;
using Contracts.Media;
using Contracts.Notifications;
using Domain.Announcing;
using Domain.Feed;
using Domain.Media;
using Domain.Notifications;

namespace Messaging.Notifications;

internal static class NotificationContextFactory
{
	public static async Task<NotificationContext> BuildAsync(
		INotification message,
		IMediaRepository mediaRepository,
		IFeedRepository feedRepository,
		CancellationToken cancellationToken
	)
	{
		var payload = ToPayload(message);
		var resolved = await ResolveAsync(payload, mediaRepository, cancellationToken);
		var feed = await ResolveFeedAsync(payload.FeedId, feedRepository, cancellationToken);
		return Build(payload, resolved, feed);
	}

	private static async Task<NotificationFeed?> ResolveFeedAsync(string? feedId, IFeedRepository feedRepository, CancellationToken cancellationToken)
	{
		if (feedId is null || !Guid.TryParse(feedId, out var guid))
		{
			return null;
		}
		return (await feedRepository.FindAsync(FeedId.From(guid), cancellationToken).Map(ToNotificationFeed)).ToNullable();
	}

	private static NotificationFeed ToNotificationFeed(Feed feed) => new(feed.Id.ToString(), feed.Name, feed.Provider, feed.Identifier);

	private static NotificationPayload ToPayload(INotification message) =>
		message switch
		{
			WantlistSyncCompletedNotification e => Payload(
				e,
				pulledAdds: e.PulledAdds,
				pulledRemoves: e.PulledRemoves,
				pushedAdds: e.PushedAdds,
				pushedRemoves: e.PushedRemoves
			),
			WantlistSyncFailedNotification e => Payload(e) with { Reason = e.Reason },
			FeedRunCompletedNotification e => Payload(e) with { FeedId = e.FeedId.ToString(), Added = e.Added, Removed = e.Removed },
			FeedRunFailedNotification e => Payload(e) with { FeedId = e.FeedId.ToString(), Reason = e.Reason },
			ProviderRateLimitedNotification e => Payload(e) with { Provider = e.Provider, RetryAfter = e.RetryAfter },
			_ => Payload(message),
		};

	private static async Task<IReadOnlyDictionary<MediaId, NotificationMedia>> ResolveAsync(
		NotificationPayload payload,
		IMediaRepository mediaRepository,
		CancellationToken cancellationToken
	)
	{
		var ids = payload.AllMediaIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return NoMedia;
		}
		var cached = await mediaRepository.GetAsync(ids, cancellationToken);
		return cached.ToDictionary(entry => entry.Identity.Id, ToNotificationMedia);
	}

	private static NotificationMedia ToNotificationMedia(StoredMedia entry)
	{
		var id = entry.Identity.Id.ToString();
		return entry.Cached.Match(
			cached => new NotificationMedia(id, cached.Details.Title, cached.Details.Type, cached.Details.ReleaseDate?.Year),
			() => new NotificationMedia(id, null, entry.Identity.Type)
		);
	}

	private static NotificationContext Build(NotificationPayload payload, IReadOnlyDictionary<MediaId, NotificationMedia> resolved, NotificationFeed? feed) =>
		new(
			payload.Id,
			payload.Type,
			payload.DefaultLevel,
			payload.OccurredAt,
			payload.Title,
			payload.Summary,
			new NotificationData
			{
				Media = BuildMedia(payload, resolved),
				Feed = feed,
				Added = payload.Added,
				Removed = payload.Removed,
				Reason = payload.Reason,
				Provider = payload.Provider,
				RetryAfter = payload.RetryAfter,
			}
		);

	private static readonly IReadOnlyDictionary<MediaId, NotificationMedia> NoMedia = new Dictionary<MediaId, NotificationMedia>();

	private static NotificationPayload Payload(
		INotification message,
		IReadOnlyList<MediaId>? pulledAdds = null,
		IReadOnlyList<MediaId>? pulledRemoves = null,
		IReadOnlyList<MediaId>? pushedAdds = null,
		IReadOnlyList<MediaId>? pushedRemoves = null,
		IReadOnlyList<MediaId>? media = null
	) =>
		new(
			message.Id,
			message.Type,
			message.DefaultLevel,
			message.OccurredAt,
			message.Type.GetDescription(),
			message.Summary,
			pulledAdds ?? [],
			pulledRemoves ?? [],
			pushedAdds ?? [],
			pushedRemoves ?? [],
			media ?? [],
			null,
			null,
			null,
			null,
			null,
			null
		);

	private static IReadOnlyList<NotificationMedia>? BuildMedia(NotificationPayload payload, IReadOnlyDictionary<MediaId, NotificationMedia> resolved)
	{
		var items = Describe(payload.PulledAdds, "Added locally", resolved)
			.Concat(Describe(payload.PulledRemoves, "Removed locally", resolved))
			.Concat(Describe(payload.PushedAdds, "Added remotely", resolved))
			.Concat(Describe(payload.PushedRemoves, "Removed remotely", resolved))
			.Concat(Describe(payload.Media, null, resolved))
			.ToList();
		return items.Count == 0 ? null : items.OrderBy(item => item.Descriptor ?? string.Empty).ToList();
	}

	private static IEnumerable<NotificationMedia> Describe(
		IReadOnlyList<MediaId> ids,
		string? descriptor,
		IReadOnlyDictionary<MediaId, NotificationMedia> resolved
	) => ids.Select(id => (resolved.GetValueOrDefault(id) ?? new NotificationMedia(id.ToString(), null, MediaType.Unknown)) with { Descriptor = descriptor });
}
