using Contracts.Identity;
using Contracts.Media.Metadata;
using Contracts.Settings;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DB.Data.Cleanup;

internal class MediaCachePurger(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	IProviderConfigRepository providerConfigRepository,
	ICurrentUser currentUser,
	ILogger<MediaCachePurger> logger
) : IMediaCachePurger
{
	public async Task PurgeAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var anchor = config.WatchlistProvider.AsMediaProvider();

		var protectedMediaIds = await ctx
			.Blacklist.IgnoreQueryFilters()
			.Select(b => b.Media.Id)
			.Union(ctx.Watchlist.IgnoreQueryFilters().Select(w => w.Media.Id))
			.Union(ctx.WatchlistOutbox.IgnoreQueryFilters().Select(o => o.Media.Id))
			.Union(ctx.FeedRunItems.IgnoreQueryFilters().Select(i => i.MediaId))
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

	public async Task PurgeMetadataAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var deleted = await ctx.MediaMetadata.ExecuteDeleteAsync(cancellationToken);
		logger.LogInformation("Media metadata purged: deleted {MetadataDeleted} metadata records", deleted);
	}
}
