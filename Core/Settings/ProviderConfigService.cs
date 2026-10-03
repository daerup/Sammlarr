using Contracts.Caching;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Settings;

namespace Core.Settings;

internal sealed class ProviderConfigService(IProviderConfigRepository repository, ITypedMemoryCache<string, ProviderConfig> cache, ICurrentUser currentUser)
	: IProviderConfigService
{
	private const string CacheKey = nameof(ProviderConfig);

	public async ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		return await cache.GetValueOrNone(CacheKey).Match(Task.FromResult, () => LoadAsync(cancellationToken));
	}

	public async Task StoreAsync(ProviderConfig config, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await repository.StoreAsync(config, cancellationToken);
		cache.Set(CacheKey, config);
	}

	private async Task<ProviderConfig> LoadAsync(CancellationToken cancellationToken)
	{
		var config = await repository.GetAsync(cancellationToken);
		cache.Set(CacheKey, config);
		return config;
	}
}
