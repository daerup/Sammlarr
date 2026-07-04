using Contracts;
using Contracts.Media.Metadata;
using Contracts.Settings;
using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DB.Data.Cleanup;

internal class MediaCachePurger(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	IProviderConfigRepository providerConfigRepository,
	ILogger<MediaCachePurger> logger
) : IMediaCachePurger
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

		var metadataDeleted = await ctx.MediaMetadata.ExecuteDeleteAsync(cancellationToken);
		var mediaDeleted = await ctx.Media.Where(m => !protectedMediaIds.Contains(m.Id)).ExecuteDeleteAsync(cancellationToken);
		var externalsDeleted = await ctx
			.ExternalMediaIds.Where(e => protectedMediaIds.Contains(e.Media.Id) && e.Source != anchor)
			.ExecuteDeleteAsync(cancellationToken);

		logger.LogInformation(
			"Media cache purged: {ProtectedCount} protected ids; deleted {MetadataDeleted} metadata, {MediaDeleted} media, {ExternalsDeleted} external ids (anchor {Anchor})",
			protectedMediaIds.Count,
			metadataDeleted,
			mediaDeleted,
			externalsDeleted,
			anchor
		);
	}
}
