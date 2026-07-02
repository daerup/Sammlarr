using Contracts.Caching;
using Contracts.Settings;
using Domain.Providers;
using Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal class ProviderConfigRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, ITypedMemoryCache<string, ProviderConfig> cache)
	: IProviderConfigRepository
{
	private const string CacheKey = nameof(ProviderConfig);

	public async ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey, out var cached))
		{
			return cached!;
		}
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.SingleAsync(cancellationToken);
		var config = new ProviderConfig
		{
			WatchlistProvider = row.WatchlistProvider,
			TrendingProvider = row.TrendingProvider,
			SearchProvider = row.SearchProvider,
			RatingProvider = row.RatingProvider,
		};
		cache.Set(CacheKey, config);
		return config;
	}

	public async Task StoreAsync(ProviderConfig config, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.SingleAsync(cancellationToken);
		row.WatchlistProvider = config.WatchlistProvider;
		row.TrendingProvider = config.TrendingProvider;
		row.SearchProvider = config.SearchProvider;
		row.RatingProvider = config.RatingProvider;
		await ctx.SaveChangesAsync(cancellationToken);
		cache.Set(CacheKey, config);
	}
}
