using Contracts.Notifications;
using Domain.Announcing;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;

namespace UI.Features.Settings.Components.Notifications;

internal static class NotificationSampleContext
{
	private static readonly IReadOnlyList<NotificationMedia> SyncedMedia =
	[
		new("sample-1", "The Matrix", MediaType.Movie, 1999, "Added locally"),
		new("sample-2", "Dune", MediaType.Movie, 2021, "Added remotely"),
		new("sample-3", "Breaking Bad", MediaType.Show, 2008, "Removed locally"),
	];

	private static readonly NotificationFeed Feed = new("sample-feed", "My Feed", FeedProvider.Mdblist, "sample-list");

	public static NotificationContext For(NotificationEventType eventType)
	{
		var sample = Sample(eventType);
		return new NotificationContext(
			sample.Id,
			sample.Type,
			sample.DefaultLevel,
			sample.OccurredAt,
			sample.Type.GetDescription(),
			sample.Summary,
			Data(eventType)
		);
	}

	private static INotification Sample(NotificationEventType eventType) =>
		eventType switch
		{
			NotificationEventType.WatchlistSyncCompleted => new WatchlistSyncCompletedNotification(SampleUser, SampleMoment, SamplePulled, [], [SampleMedia], [], []),
			NotificationEventType.WatchlistSyncFailed => new WatchlistSyncFailedNotification(SampleUser, SampleMoment, "Provider timed out"),
			NotificationEventType.FeedRunCompleted => new FeedRunCompletedNotification(SampleUser, SampleMoment, SampleFeed, 2, 1),
			NotificationEventType.FeedRunFailed => new FeedRunFailedNotification(SampleUser, SampleMoment, SampleFeed, "Provider timed out"),
			NotificationEventType.ProviderRateLimited => new ProviderRateLimitedNotification(SampleUser, SampleMoment, MediaProvider.Tmdb, TimeSpan.FromSeconds(30)),
			NotificationEventType.TestPing => new TestPingNotification(SampleUser, SampleMoment, Guid.Empty),
		};

	private static NotificationData Data(NotificationEventType eventType) =>
		eventType switch
		{
			NotificationEventType.WatchlistSyncCompleted => new NotificationData { Media = SyncedMedia },
			NotificationEventType.WatchlistSyncFailed => new NotificationData { Reason = "Provider timed out" },
			NotificationEventType.FeedRunCompleted => new NotificationData
			{
				Feed = Feed,
				Added = 2,
				Removed = 1,
			},
			NotificationEventType.FeedRunFailed => new NotificationData { Feed = Feed, Reason = "Provider timed out" },
			NotificationEventType.ProviderRateLimited => new NotificationData { Provider = MediaProvider.Tmdb, RetryAfter = TimeSpan.FromSeconds(30) },
			NotificationEventType.TestPing => new NotificationData(),
		};

	private static readonly Guid SampleId = new("11111111-1111-1111-1111-111111111111");

	private static readonly UserId SampleUser = UserId.From(SampleId);

	private static readonly DateTimeOffset SampleMoment = DateTimeOffset.UnixEpoch;

	private static readonly FeedId SampleFeed = FeedId.From(SampleId);

	private static readonly MediaId SampleMedia = MediaId.From(SampleId);
	private static readonly IReadOnlyList<MediaId> SamplePulled = [SampleMedia, SampleMedia];
}
