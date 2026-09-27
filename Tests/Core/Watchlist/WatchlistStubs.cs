using Contracts;
using Contracts.Announcing;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Core.Announcing;
using Core.Watchlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Watchlist;

internal static class WatchlistStubs
{
	public static WatchlistSyncPlanner Planner(Mock<IWatchlistProvider> provider, Mock<IWatchlistOutbox> outbox) =>
		new(
			ProviderConfigStubs.PlexWatchlist().Object,
			EmptyRepository().Object,
			outbox.Object,
			UnresolvedMedia().Object,
			new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object),
			NullLogger<WatchlistSyncPlanner>.Instance
		);

	public static Mock<IWatchlistRepository> EmptyRepository()
	{
		var repository = IWatchlistRepository.Mock();
		repository.GetIdsAsync(Any()).Returns([]);
		repository.AddAsync(Any<IEnumerable<MediaId>>(), Any<WatchlistItemProvenance>(), Any()).Returns([]);
		repository.RemoveAsync(Any<IEnumerable<MediaId>>(), Any()).Returns([]);
		return repository;
	}

	public static Mock<IWatchlistOutbox> EmptyOutbox()
	{
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([]);
		return outbox;
	}

	public static Mock<IWatchlistOutbox> OutboxPending(IReadOnlyList<MediaId> mediaIds)
	{
		var outbox = IWatchlistOutbox.Mock();
		outbox.GetAsync(Any()).Returns([.. mediaIds.Select(id => new WatchlistOutboxEntry(id, WatchlistOperationAction.Add, WatchlistOutboxState.Pending, 0))]);
		return outbox;
	}

	public static WatchlistSyncEngine Engine(
		Mock<IWatchlistProvider> provider,
		Mock<IWatchlistOutbox>? outbox = null,
		IReadOnlyDictionary<MediaId, IExternalMediaId>? externals = null,
		Mock<IEventPublisher>? events = null,
		WatchlistSyncGate? gate = null,
		ICurrentUser? user = null,
		IMessagePublisher? publisher = null
	)
	{
		outbox ??= EmptyOutbox();
		var owner = user ?? new TestCurrentUser();
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
		var media = IMediaRepository.Mock();
		media.GetExternalsAsync(Any<IEnumerable<MediaId>>(), Any<MediaProvider>(), Any()).Returns(externals ?? new Dictionary<MediaId, IExternalMediaId>());

		return new WatchlistSyncEngine(
			gate ?? new WatchlistSyncGate(),
			Planner(provider, outbox),
			ProviderConfigStubs.PlexWatchlist().Object,
			EmptyRepository().Object,
			outbox.Object,
			media.Object,
			new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object),
			new Announcer((events ?? IEventPublisher.Mock()).Object, publisher ?? new RecordingPublisher()),
			owner,
			clock.Object,
			NullLogger<WatchlistSyncEngine>.Instance
		);
	}

	public static Mock<IMediaService> UnresolvedMedia()
	{
		var media = IMediaService.Mock();
		media.IdentifyAsync(Any<IEnumerable<ResolveRequest>>(), Any()).Returns((requests, _) => AsAsyncEnumerable(requests.Select(Unresolved).ToList()));
		return media;
	}

	public static async IAsyncEnumerable<UnmatchedMedia> NothingRemote()
	{
		await Task.CompletedTask;
		yield break;
	}

	private static Result<MediaIdentity> Unresolved(ResolveRequest request) => Error.From("unresolved");

	private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(IReadOnlyList<T> items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.CompletedTask;
		}
	}
}
