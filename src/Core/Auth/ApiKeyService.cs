using Contracts.Auth;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Jobs;
using Domain.Auth;
using Domain.Jobs;
using Domain.Providers;
using Domain.Usage;
using Humanizer;
using Observability.Usage;

namespace Core.Auth;

internal sealed class ApiKeyService(
	IApiKeyConfigRepository keys,
	IKeyedServiceIndex<ApiKeyProvider, IApiKeyValidator> validators,
	IFeedService feeds,
	SecretCache<ApiKeyProvider, IApiKey> cache,
	IJobQueue queue
) : IApiKeyService
{
	public ValueTask<Result<IApiKey>> GetAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		return cache.GetOrLoadAsync(
			provider,
			() => keys.GetAsync(provider, cancellationToken).Map(value => KeyOf(provider, value)),
			key => CheckStoredAsync(provider, key, cancellationToken)
		);
	}

	private async Task<AccessCheck> CheckStoredAsync(ApiKeyProvider provider, IApiKey key, CancellationToken cancellationToken)
	{
		using var marked = UsageCauses.Mark(UsageCause.KeyTest);
		return await validators[provider].CheckAsync(key.Value, cancellationToken);
	}

	public Task<bool> IsStoredAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		return keys.GetAsync(provider, cancellationToken).IsSuccess();
	}

	public async Task<Result<IApiKey>> TestAsync(ApiKeyProvider provider, Secret value, CancellationToken cancellationToken = default)
	{
		using var marked = UsageCauses.Mark(UsageCause.KeyTest);
		var check = await validators[provider].CheckAsync(value, cancellationToken);
		return AccessChecks.Answer(check, KeyOf(provider, value), provider.Remote.Humanize(), "API key");
	}

	public async Task StoreAsync(IApiKey key, CancellationToken cancellationToken = default)
	{
		await keys.StoreAsync(key.Provider, key.Value, cancellationToken);
		cache.Put(key.Provider, key);
		await queue.EnqueueAsync(new FillDetails(), cancellationToken);
	}

	public async Task ClearAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await keys.ClearAsync(provider, cancellationToken);
		cache.Evict(provider);
		await feeds.PauseFeedsOfAsync(provider.Remote, cancellationToken);
	}

	public void ReportRejected(ApiKeyProvider provider)
	{
		cache.Reject(provider);
	}

	private static IApiKey KeyOf(ApiKeyProvider provider, Secret value) => new StoredApiKey(provider, value);
}
