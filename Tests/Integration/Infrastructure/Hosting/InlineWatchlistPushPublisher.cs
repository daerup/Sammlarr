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

namespace Integration.Tests.Infrastructure.Hosting;

internal sealed class InlineWatchlistPushPublisher(IServiceProvider services) : IMessagePublisher
{
	public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
		where TMessage : notnull
	{
		if (message is not PushWatchlistOperation push)
		{
			return;
		}

		try
		{
			await new WatchlistPushHandler().Handle(
				push,
				services.GetRequiredService<IUserScope>(),
				services.GetRequiredService<IWatchlistOutbox>(),
				services.GetRequiredService<IProviderConfigRepository>(),
				services.GetRequiredService<IMediaRepository>(),
				services.GetRequiredService<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(),
				cancellationToken
			);
		}
		catch (WatchlistPushFailedException) { }
	}
}
