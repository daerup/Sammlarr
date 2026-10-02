using Contracts.Auth;
using Contracts.Feed;
using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed class ApiKeyService(IApiKeyConfigRepository keys, IApiKeyFactory factory, IFeedService feeds, SecretCache<ApiKeyProvider, IApiKey> cache)
	: IApiKeyService
{
	public ValueTask<Result<IApiKey>> GetAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default) =>
		cache.GetOrLoadAsync(provider, () => keys.GetAsync(provider, cancellationToken).Map(value => factory.CreateAsync(provider, value, cancellationToken)));

	public async Task StoreAsync(IApiKey key, CancellationToken cancellationToken = default)
	{
		await keys.StoreAsync(key.Provider, key.Value, cancellationToken);
		cache.Put(key.Provider, key);
	}

	public async Task ClearAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await keys.ClearAsync(provider, cancellationToken);
		cache.Evict(provider);
		await feeds.PauseAsync(provider, cancellationToken);
	}

	public void ReportRejected(ApiKeyProvider provider) => cache.Evict(provider);
}
