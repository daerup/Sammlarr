using Contracts.Feed;
using Contracts.Media;
using Contracts.Notifications;
using Domain.Feed;
using Domain.Media;
using Domain.Notifications.Events;

namespace Messaging.Notifications;

internal static class NotificationContextFactory
{
	public static async Task<NotificationContext> BuildAsync(
		BaseNotification message,
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
		var feed = await feedRepository.GetFeedByIdAsync(FeedId.From(guid), cancellationToken);
		return feed is null ? null : new NotificationFeed(feed.Id.ToString(), feed.Name, feed.Provider, feed.Identifier);
	}

	private static NotificationPayload ToPayload(BaseNotification message) =>
		message switch
		{
			WatchlistSyncedEvent e => Payload(e, pulledAdds: e.PulledAdds, pulledRemoves: e.PulledRemoves, pushedAdds: e.PushedAdds, pushedRemoves: e.PushedRemoves),
			FeedRunCompletedEvent e => Payload(e) with { FeedId = e.FeedId.ToString(), Added = e.Added, Removed = e.Removed },
			FeedRunFailedEvent e => Payload(e) with { FeedId = e.FeedId.ToString(), Reason = e.Reason },
			ProviderRateLimitedEvent e => Payload(e) with { Provider = e.Provider, RetryAfter = e.RetryAfter },
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
		return cached.ToDictionary(
			entry => entry.Stored.Id,
			entry => new NotificationMedia(entry.Stored.Id.ToString(), entry.Stored.Title, entry.Stored.Type, entry.Stored.ReleaseDate?.Year)
		);
	}

	private static NotificationContext Build(NotificationPayload payload, IReadOnlyDictionary<MediaId, NotificationMedia> resolved, NotificationFeed? feed) =>
		new(
			payload.Id,
			payload.Type,
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
		BaseNotification message,
		IReadOnlyList<MediaId>? pulledAdds = null,
		IReadOnlyList<MediaId>? pulledRemoves = null,
		IReadOnlyList<MediaId>? pushedAdds = null,
		IReadOnlyList<MediaId>? pushedRemoves = null,
		IReadOnlyList<MediaId>? media = null
	) =>
		new(
			message.Id,
			message.Type,
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
