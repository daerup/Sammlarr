using Contracts.Caching;
using Contracts.Identity;
using Contracts.Settings;

namespace Core.Settings;

internal sealed class ArrSinkConfigService(
	IArrSinkConfigRepository repository,
	ITypedMemoryCache<string, ArrSinkConfig> cache,
	IUserRepository users,
	ICurrentUser currentUser
) : IArrSinkConfigService
{
	private const string CacheKey = nameof(ArrSinkConfig);

	public async Task<ArrSinkConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		var config = await cache.GetValueOrNone(CacheKey).Match(Task.FromResult, () => LoadAsync(cancellationToken));
		var syncing = await users.GetAllAsync(cancellationToken);
		return config with { Tags = config.Tags with { PerUser = config.Tags.PerUser || syncing.Count > 1 } };
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
