using Contracts.Caching;
using Contracts.Identity;
using Contracts.Settings;

namespace Core.Settings;

internal sealed class ArrSinkConfigService(IArrSinkConfigRepository repository, ITypedMemoryCache<string, ArrSinkConfig> cache, ICurrentUser currentUser)
	: IArrSinkConfigService
{
	private const string CacheKey = nameof(ArrSinkConfig);

	public async Task<ArrSinkConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		return await cache.GetValueOrNone(CacheKey).Match(Task.FromResult, () => LoadAsync(cancellationToken));
	}

	public async Task StoreAsync(ArrSinkConfig config, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await repository.StoreAsync(config, cancellationToken);
		cache.Set(CacheKey, config);
	}

	private async Task<ArrSinkConfig> LoadAsync(CancellationToken cancellationToken)
	{
		var config = await repository.GetAsync(cancellationToken);
		cache.Set(CacheKey, config);
		return config;
	}
}
