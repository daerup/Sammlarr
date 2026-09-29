using Contracts.Auth;
using Contracts.Feed;
using Domain.Providers;

namespace Core.Auth;

internal sealed class ApiKeyService(IApiKeyConfigRepository keys, IFeedService feeds) : IApiKeyService
{
	public async Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await keys.RemoveApiKeyAsync(provider, cancellationToken);
		await feeds.DisableFeedsUsingAsync(provider, cancellationToken);
	}
}
