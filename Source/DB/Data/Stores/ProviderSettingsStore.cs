using Contracts.Caching;
using Contracts.Settings;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

internal class ProviderSettingsStore(SammlarrDbContext context, ITypedMemoryCache<string, ProviderSettings> cache) : IProviderSettingsStore
{
	private const string CacheKey = nameof(ProviderSettingsEntity);

	public async ValueTask<ProviderSettings> GetAsync(CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey, out var cached))
		{
			return cached!;
		}
		var settings = await context.ProviderSettings.SingleAsync(cancellationToken);
		var dto = new ProviderSettings
		{
			WatchlistProvider = settings.WatchlistProvider,
			TrendingProvider = settings.TrendingProvider,
			SearchProvider = settings.SearchProvider,
		};
		cache.Set(CacheKey, dto);
		return dto;
	}

	public async Task StoreAsync(ProviderSettings providerSettings, CancellationToken cancellationToken = default)
	{
		var settings = await context.ProviderSettings.SingleAsync(cancellationToken);
		settings.WatchlistProvider = providerSettings.WatchlistProvider;
		settings.TrendingProvider = providerSettings.TrendingProvider;
		settings.SearchProvider = providerSettings.SearchProvider;
		await context.SaveChangesAsync(cancellationToken);
		cache.Set(CacheKey, providerSettings);
	}
}
