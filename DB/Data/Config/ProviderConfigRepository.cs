using Contracts.Caching;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal sealed class ProviderConfigRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	ITypedMemoryCache<string, ProviderConfig> cache,
	ICurrentUser currentUser
) : IProviderConfigRepository
{
	private string CacheKey => $"{(currentUser.IsAuthenticated ? currentUser.Id.Value : Guid.Empty)}:{nameof(ProviderConfig)}";

	public async ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey, out var cached))
		{
			return cached!;
		}
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.SingleOrDefaultAsync(cancellationToken) ?? ProviderConfigRow.Default;
		var config = new ProviderConfig
		{
			WatchlistProvider = row.WatchlistProvider,
			TrendingProvider = row.TrendingProvider,
			SearchProvider = row.SearchProvider,
			RatingProvider = row.RatingProvider,
			AuthProvider = row.AuthProvider,
		};
		cache.Set(CacheKey, config);
		return config;
	}

	public async Task StoreAsync(ProviderConfig config, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.SingleOrDefaultAsync(cancellationToken);
		if (row is null)
		{
			ctx.ProviderSettings.Add(
				new ProviderConfigRow
				{
					WatchlistProvider = config.WatchlistProvider,
					TrendingProvider = config.TrendingProvider,
					SearchProvider = config.SearchProvider,
					RatingProvider = config.RatingProvider,
					AuthProvider = config.AuthProvider,
				}
			);
		}
		else
		{
			row.WatchlistProvider = config.WatchlistProvider;
			row.TrendingProvider = config.TrendingProvider;
			row.SearchProvider = config.SearchProvider;
			row.RatingProvider = config.RatingProvider;
			row.AuthProvider = config.AuthProvider;
		}

		await ctx.SaveChangesAsync(cancellationToken);
		cache.Set(CacheKey, config);
	}
}
