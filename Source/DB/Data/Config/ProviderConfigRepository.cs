using Contracts.Caching;
using Contracts.Settings;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal class ProviderConfigRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, ITypedMemoryCache<string, ProviderConfig> cache)
	: IProviderConfigRepository
{
	private const string CacheKey = nameof(ProviderConfigEntity);

	public async ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey, out var cached))
		{
			return cached!;
		}
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var settings = await ctx.ProviderSettings.SingleAsync(cancellationToken);
		var dto = new ProviderConfig
		{
			WatchlistProvider = settings.WatchlistProvider,
			TrendingProvider = settings.TrendingProvider,
			SearchProvider = settings.SearchProvider,
			RatingProvider = settings.RatingProvider,
		};
		cache.Set(CacheKey, dto);
		return dto;
	}

	public async Task StoreAsync(ProviderConfig providerConfig, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var settings = await ctx.ProviderSettings.SingleAsync(cancellationToken);
		settings.WatchlistProvider = providerConfig.WatchlistProvider;
		settings.TrendingProvider = providerConfig.TrendingProvider;
		settings.SearchProvider = providerConfig.SearchProvider;
		settings.RatingProvider = providerConfig.RatingProvider;
		await ctx.SaveChangesAsync(cancellationToken);
		cache.Set(CacheKey, providerConfig);
	}
}
