using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed class ApiKeyService(
	IApiKeyConfigRepository keys,
	IKeyedServiceIndex<ApiKeyProvider, IApiKeyValidator> validators,
	IFeedService feeds,
	SecretCache<ApiKeyProvider, IApiKey> cache
) : IApiKeyService
{
	public ValueTask<Result<IApiKey>> GetAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		return cache.GetOrLoadAsync(
			provider,
			() => keys.GetAsync(provider, cancellationToken).Map(value => KeyOf(provider, value)),
			key => validators[provider].CheckAsync(key.Value, cancellationToken)
		);
	}

	public Task<bool> IsStoredAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		return keys.GetAsync(provider, cancellationToken).IsSuccess();
	}

	public async Task<Result<IApiKey>> TestAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default)
	{
		var check = await validators[provider].CheckAsync(value, cancellationToken);
		return AccessChecks.Answer(check, KeyOf(provider, value), provider.Remote.GetDescription(), "API key");
	}

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

	public void ReportRejected(ApiKeyProvider provider)
	{
		cache.Reject(provider);
	}

	private static IApiKey KeyOf(ApiKeyProvider provider, string value) => new StoredApiKey(provider, value);
}
