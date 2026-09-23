using Contracts.Media;
using Contracts.Messaging;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Core.Watchlist.Sync;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
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

	internal sealed class NoopPublisher : IMessagePublisher
	{
		public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
			where TMessage : notnull => Task.CompletedTask;
	}
}
