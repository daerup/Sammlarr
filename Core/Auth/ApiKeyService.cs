using Contracts.Auth;
using Contracts.Caching;
using Contracts.Feed;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed class ApiKeyService(
	IApiKeyConfigRepository keys,
	IApiKeyFactory factory,
	IFeedService feeds,
	ITypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey> cache,
	ICurrentUser currentUser
) : IApiKeyService
{
	private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

	private (Guid User, ApiKeyProvider Provider) CacheKey(ApiKeyProvider provider) => (currentUser.IsAuthenticated ? currentUser.Id.Value : Guid.Empty, provider);

	public async ValueTask<Result<IApiKey>> GetApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey(provider), out var cached) && cached is not null)
		{
			return Success.Of(cached);
		}

		return await keys.GetApiKeyAsync(provider, cancellationToken)
			.Map(value => factory.CreateAsync(provider, value, cancellationToken))
			.Apply(apiKey => cache.Set(CacheKey(provider), apiKey, CacheLifetime));
	}

	public async Task<bool> HasApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default) =>
		await GetApiKeyAsync(provider, cancellationToken).IsSuccess();

	public async Task<IReadOnlySet<FeedProvider>> GetFeedProvidersWithoutKeyAsync(CancellationToken cancellationToken = default)
	{
		var missing = new HashSet<FeedProvider>();
		foreach (var provider in Enum.GetValues<FeedProvider>())
		{
			if (!await HasApiKeyAsync(provider.AsApiKeyProvider(), cancellationToken))
			{
				missing.Add(provider);
			}
		}

		return missing;
	}

	public Task<Result<IApiKey>> TestApiKeyAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default) =>
		factory.CreateAsync(provider, value, cancellationToken);

	public async Task SaveApiKeyAsync(IApiKey apiKey, CancellationToken cancellationToken = default)
	{
		await keys.SetApiKeyAsync(apiKey.Provider, apiKey.Value, cancellationToken);
		cache.Set(CacheKey(apiKey.Provider), apiKey, CacheLifetime);
	}

	public async Task RemoveApiKeyAsync(ApiKeyProvider provider, CancellationToken cancellationToken = default)
	{
		await keys.RemoveApiKeyAsync(provider, cancellationToken);
		cache.Remove(CacheKey(provider));
		await feeds.DisableFeedsUsingAsync(provider, cancellationToken);
	}

	public void Invalidate(ApiKeyProvider provider) => cache.Remove(CacheKey(provider));
}
