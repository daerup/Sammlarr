using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using DB.Data.Media;
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
	IDateTimeProvider clock,
	ILogger<MediaCachePurger> logger
) : IMediaCachePurger
{
	public async Task PurgeAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var sink = sinks[config.Sink];

		var protectedMediaIds = await ctx.ProtectedMediaIds().ToListAsync(cancellationToken);

		var detailsDeleted = await ctx.TrimDetailsAsync(ctx.MediaDetails, clock.Now, cancellationToken);
		var mediaDeleted = await ctx.Media.Where(m => !protectedMediaIds.Contains(m.Id)).ExecuteDeleteAsync(cancellationToken);
		var externalsDeleted = 0;
		foreach (var type in Enum.GetValues<MediaType>())
		{
			if (sink.AddressedBy(type) is not Some<MediaProvider>(var anchor))
			{
				continue;
			}
			externalsDeleted += await ctx
				.ExternalMediaIds.Where(e =>
					protectedMediaIds.Contains(e.Media.Id)
					&& e.MediaType == type
					&& e.Source != anchor
					&& ctx.ExternalMediaIds.Any(kept => kept.Media.Id == e.Media.Id && kept.Source == anchor)
				)
				.ExecuteDeleteAsync(cancellationToken);
		}

		logger.LogInformation(
			"Media cache purged: {ProtectedCount} protected ids; deleted {DetailsDeleted} details, {MediaDeleted} media, {ExternalsDeleted} external ids (kept the ids the {Sink} sink addresses each type by)",
			protectedMediaIds.Count,
			detailsDeleted,
			mediaDeleted,
			externalsDeleted,
			config.Sink
		);
	}

	public async Task PurgeDetailsAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var deleted = await ctx.TrimDetailsAsync(ctx.MediaDetails, clock.Now, cancellationToken);
		logger.LogInformation("Media details purged: deleted {DetailsDeleted} details records", deleted);
	}
}
