using Contracts.DI;
using Contracts.Identity;
using Contracts.Media.Metadata;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DB.Data.Cleanup;

internal class MediaCachePurger(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	IProviderConfigRepository providerConfigRepository,
	IKeyedServiceIndex<SinkKind, IMediaSink> sinks,
	ICurrentUser currentUser,
	ILogger<MediaCachePurger> logger
) : IMediaCachePurger
{
	public async Task PurgeAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var sink = sinks[config.Sink];

		var protectedMediaIds = await ctx
			.Blocklist.IgnoreQueryFilters()
			.Select(b => b.Media.Id)
			.Union(ctx.Wantlist.IgnoreQueryFilters().Select(w => w.Media.Id))
			.Union(ctx.WantlistOutbox.IgnoreQueryFilters().Select(o => o.Media.Id))
			.Union(ctx.FeedRunItems.IgnoreQueryFilters().Select(i => i.MediaId))
			.ToListAsync(cancellationToken);

		var metadataDeleted = await ctx.MediaDetails.ExecuteDeleteAsync(cancellationToken);
		var mediaDeleted = await ctx.Media.Where(m => !protectedMediaIds.Contains(m.Id)).ExecuteDeleteAsync(cancellationToken);
		var externalsDeleted = 0;
		foreach (var type in Enum.GetValues<MediaType>())
		{
			if (sink.AddressedBy(type) is not Some<MediaProvider>(var anchor))
			{
				continue;
			}
			externalsDeleted += await ctx
				.ExternalMediaIds.Where(e => protectedMediaIds.Contains(e.Media.Id) && e.MediaType == type && e.Source != anchor)
				.ExecuteDeleteAsync(cancellationToken);
		}

		logger.LogInformation(
			"Media cache purged: {ProtectedCount} protected ids; deleted {MetadataDeleted} metadata, {MediaDeleted} media, {ExternalsDeleted} external ids (kept the ids the {Sink} sink addresses each type by)",
			protectedMediaIds.Count,
			metadataDeleted,
			mediaDeleted,
			externalsDeleted,
			config.Sink
		);
	}

	public async Task PurgeMetadataAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var deleted = await ctx.MediaDetails.ExecuteDeleteAsync(cancellationToken);
		logger.LogInformation("Media metadata purged: deleted {MetadataDeleted} metadata records", deleted);
	}
}
