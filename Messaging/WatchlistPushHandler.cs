using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist.Outbox;

namespace Messaging;

public sealed class WatchlistPushHandler
{
	private const int MaxAttempts = 5;

	public async Task Handle(
		PushWatchlistOperation message,
		IUserScope userScope,
		IWatchlistOutbox outbox,
		IProviderConfigRepository providerConfig,
		IMediaRepository mediaRepository,
		IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider> watchlistProviders,
		CancellationToken cancellationToken
	)
	{
		using var scope = await userScope.AsAsync(message.UserId, cancellationToken);

		var pending = await outbox.FindPendingAsync(message.MediaId, cancellationToken);
		if (pending is null)
		{
			return;
		}

		var provider = (await providerConfig.GetAsync(cancellationToken)).WatchlistProvider;
		var externals = await mediaRepository.GetExternalsAsync([message.MediaId], provider.AsMediaProvider(), cancellationToken);
		if (!externals.TryGetValue(message.MediaId, out var external))
		{
			await outbox.RecordFailureAsync([message.MediaId], WatchlistDeliveryStatus.Failed, cancellationToken);
			return;
		}

		if (await PushAsync(watchlistProviders[provider], pending.Action, external, cancellationToken))
		{
			await outbox.DropAsync([message.MediaId], cancellationToken);
			return;
		}

		var giveUp = pending.Attempts + 1 >= MaxAttempts;
		await outbox.RecordFailureAsync([message.MediaId], giveUp ? WatchlistDeliveryStatus.Failed : WatchlistDeliveryStatus.Pending, cancellationToken);
		if (!giveUp)
		{
			throw new WatchlistPushFailedException(message.MediaId, pending.Action);
		}
	}

	private static async Task<bool> PushAsync(
		IWatchlistProvider provider,
		WatchlistOperationAction action,
		IExternalMediaId external,
		CancellationToken cancellationToken
	)
	{
		var results = action switch
		{
			WatchlistOperationAction.Add => provider.AddToWatchlistAsync([external], cancellationToken),
			WatchlistOperationAction.Remove => provider.RemoveFromWatchlistAsync([external], cancellationToken),
		};

		await foreach (var result in results.WithCancellation(cancellationToken))
		{
			return result.Success;
		}
		return false;
	}
}
