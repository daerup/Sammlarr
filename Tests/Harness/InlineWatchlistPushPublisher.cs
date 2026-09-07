using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Providers;
using Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Harness;

internal sealed class PublishedMessages
{
	private readonly List<object> _messages = [];
	private readonly Lock _gate = new();

	internal void Record(object message)
	{
		lock (_gate)
		{
			_messages.Add(message);
		}
	}

	internal IReadOnlyList<T> Recorded<T>()
	{
		lock (_gate)
		{
			return [.. _messages.OfType<T>()];
		}
	}
}

internal sealed class InlineWatchlistPushPublisher(IServiceProvider services, PublishedMessages published) : IMessagePublisher
{
	public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
		where TMessage : notnull
	{
		published.Record(message);

		if (message is not WatchlistPushRequest push)
		{
			return;
		}

		try
		{
			await WatchlistPushRequestHandler.Handle(
				push,
				services.GetRequiredService<IUserScope>(),
				services.GetRequiredService<IWatchlistOutbox>(),
				services.GetRequiredService<IProviderConfigRepository>(),
				services.GetRequiredService<IMediaRepository>(),
				services.GetRequiredService<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(),
				services.GetRequiredService<IWatchlistOutboxEventFeed>(),
				cancellationToken
			);
		}
		catch (WatchlistPushFailedException) { }
	}
}
