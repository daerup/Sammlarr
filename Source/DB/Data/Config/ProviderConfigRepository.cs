using Contracts.Caching;
using Contracts.Settings;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal class ProviderConfigRepository(SammlarrDbContext context, ITypedMemoryCache<string, ProviderConfig> cache) : IProviderConfigRepository
{
	private const string CacheKey = nameof(ProviderConfigEntity);

	public async ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey, out var cached))
		{
			return cached!;
		}
		var settings = await context.ProviderSettings.SingleAsync(cancellationToken);
		var dto = new ProviderConfig
		{
			WatchlistProvider = settings.WatchlistProvider,
			TrendingProvider = settings.TrendingProvider,
			SearchProvider = settings.SearchProvider,
		};
		cache.Set(CacheKey, dto);
		return dto;
	}

	public async Task StoreAsync(ProviderConfig providerConfig, CancellationToken cancellationToken = default)
	{
		var settings = await context.ProviderSettings.SingleAsync(cancellationToken);
		settings.WatchlistProvider = providerConfig.WatchlistProvider;
		settings.TrendingProvider = providerConfig.TrendingProvider;
		settings.SearchProvider = providerConfig.SearchProvider;
		await context.SaveChangesAsync(cancellationToken);
		cache.Set(CacheKey, providerConfig);
	}
}
