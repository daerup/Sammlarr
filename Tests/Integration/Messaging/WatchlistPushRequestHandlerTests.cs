using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Watchlist.Outbox;
using Messaging;
using TestInfrastructure;

namespace Integration.Tests.Messaging;

public class WatchlistPushRequestHandlerTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Pending_add_that_pushes_successfully_is_dropped()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-1", MediaType.Movie);

		var outbox = OutboxWith(new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 0));
		var provider = ProviderPushing(external, success: true);
		var feed = IWatchlistOutboxEventFeed.Mock();

		await Handle(new WatchlistPushRequest(User, mediaId), outbox, provider, Externals(mediaId, external), feed: feed);

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Once);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistOutboxState>(), Any()).WasCalled(Times.Never);
		feed.Publish(new WatchlistOutboxEvent(User, mediaId, WatchlistOutboxEventKind.Pushed)).WasCalled(Times.Once);
	}

	[Test]
	public async Task No_pending_op_is_a_no_op()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var outbox = OutboxWith(null);
		var provider = IWatchlistProvider.Mock();

		await Handle(new WatchlistPushRequest(User, mediaId), outbox, provider, Externals());

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Op_with_no_external_id_is_marked_failed_without_a_push()
	{
		var mediaId = MediaId.From(Guid.NewGuid());

		var outbox = OutboxWith(new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 0));
		var provider = IWatchlistProvider.Mock();

		await Handle(new WatchlistPushRequest(User, mediaId), outbox, provider, Externals());

		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasCalled(Times.Never);
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WatchlistOutboxState.Failed, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Failed_push_below_max_attempts_records_failure_and_throws_for_retry()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-2", MediaType.Movie);

		var outbox = OutboxWith(new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Remove, WatchlistOutboxState.Pending, 0));
		var provider = ProviderPushing(external, success: false);

		var threw = false;
		try
		{
			await Handle(new WatchlistPushRequest(User, mediaId), outbox, provider, Externals(mediaId, external));
		}
		catch (WatchlistPushFailedException)
		{
			threw = true;
		}

		await Assert.That(threw).IsTrue();
		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WatchlistOutboxState.Pending, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Failed_push_at_max_attempts_is_marked_failed_without_throwing()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-3", MediaType.Movie);

		var outbox = OutboxWith(new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 4));
		var provider = ProviderPushing(external, success: false);

		await Handle(new WatchlistPushRequest(User, mediaId), outbox, provider, Externals(mediaId, external));

		outbox.RecordFailedAttemptAsync(Any<IEnumerable<MediaId>>(), WatchlistOutboxState.Failed, Any()).WasCalled(Times.Once);
		outbox.DropAsync(Any<IEnumerable<MediaId>>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Handler_establishes_the_user_scope_for_the_message_user()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = PlexId.From("plex-9", MediaType.Movie);
		var outbox = OutboxWith(new WatchlistOutboxEntry(mediaId, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 0));
		var provider = ProviderPushing(external, success: true);

		var userScope = IUserScope.Mock();
		userScope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());

		await Handle(new WatchlistPushRequest(User, mediaId), outbox, provider, Externals(mediaId, external), userScope);

		userScope.AsAsync(User, Any()).WasCalled(Times.Once);
	}

	private static Mock<IWatchlistOutbox> OutboxWith(WatchlistOutboxEntry? pending)
	{
		var outbox = IWatchlistOutbox.Mock();
		outbox.FindPendingAsync(Any<MediaId>(), Any()).Returns(pending);
		return outbox;
	}

	private static Mock<IWatchlistProvider> ProviderPushing(IExternalMediaId external, bool success)
	{
		var provider = IWatchlistProvider.Mock();
		provider.AddToWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(new WatchlistPushAck(external, success)));
		provider.RemoveFromWatchlistAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).Returns(Stream(new WatchlistPushAck(external, success)));
		return provider;
	}

	private static async Task Handle(
		WatchlistPushRequest message,
		Mock<IWatchlistOutbox> outbox,
		Mock<IWatchlistProvider> provider,
		IReadOnlyDictionary<MediaId, IExternalMediaId> externals,
		Mock<IUserScope>? userScope = null,
		Mock<IWatchlistOutboxEventFeed>? feed = null
	)
	{
		if (userScope is null)
		{
			userScope = IUserScope.Mock();
			userScope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());
		}

		feed ??= IWatchlistOutboxEventFeed.Mock();

		var config = IProviderConfigRepository.Mock();
		config
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = default,
					SearchProvider = default,
					RatingProvider = default,
					AuthProvider = default,
					RegistrationMode = default,
				}
			);

		var mediaRepo = IMediaRepository.Mock();
		mediaRepo.GetExternalsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any()).Returns(externals);

		var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);

		await WatchlistPushRequestHandler.Handle(
			message,
			userScope.Object,
			outbox.Object,
			config.Object,
			mediaRepo.Object,
			index,
			feed.Object,
			CancellationToken.None
		);
	}

	private static IReadOnlyDictionary<MediaId, IExternalMediaId> Externals(MediaId id, IExternalMediaId external) =>
		new Dictionary<MediaId, IExternalMediaId> { [id] = external };

	private static IReadOnlyDictionary<MediaId, IExternalMediaId> Externals() => new Dictionary<MediaId, IExternalMediaId>();

	private static async IAsyncEnumerable<T> Stream<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
		}
		await Task.CompletedTask;
	}

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose() { }
	}
}
