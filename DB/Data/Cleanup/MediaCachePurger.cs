using Contracts;
using Contracts.Media.Metadata;
using Contracts.Settings;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Cleanup;

internal class MediaCachePurger(IDbContextFactory<SammlarrDbContext> dbContextFactory, IProviderConfigRepository providerConfigRepository) : IMediaCachePurger
{
	public async Task PurgeAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var anchor = config.WatchlistProvider.AsMediaProvider();

		var protectedMediaIds = await ctx
			.Blacklist.Select(b => b.Media.Id)
			.Union(ctx.Watchlist.Select(w => w.Media.Id))
			.Union(ctx.WatchlistOutbox.Select(o => o.Media.Id))
			.ToListAsync(cancellationToken);

		await ctx.MediaMetadata.ExecuteDeleteAsync(cancellationToken);
		await ctx.Media.Where(m => !protectedMediaIds.Contains(m.Id)).ExecuteDeleteAsync(cancellationToken);
		await ctx.ExternalMediaIds.Where(e => protectedMediaIds.Contains(e.Media.Id) && e.Source != anchor).ExecuteDeleteAsync(cancellationToken);
	}
}
