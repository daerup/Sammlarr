using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Metadata;

internal class MediaMetadataRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, MediaMetadataFactory metadataFactory) : IMediaMetadataRepository
{
	public async Task<CachedMediaMetadata?> GetByMediaIdAsync(MediaId mediaId, CancellationToken cancellationToken = default)
	{
		var raw = mediaId.Value;
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx
			.MediaMetadata.AsNoTracking()
			.Include(m => m.Media)
				.ThenInclude(media => media.ExternalIds)
			.Include(m => m.Ratings)
			.SingleOrDefaultAsync(m => m.MediaId == raw, cancellationToken);

		if (entity is null)
		{
			return null;
		}

		var externals = entity.Media.ExternalIds.Select(externalId => externalId.ToExternalMediaId()).ToList();
		return new CachedMediaMetadata(metadataFactory.ToDetailResult(entity, mediaId, externals), entity.LastFetchedAt);
	}

	public async Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default)
	{
		if (items.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var rawIds = items.Select(item => item.MediaId.Value).Distinct().ToList();
		var existingByMediaId = await ctx
			.MediaMetadata.Include(m => m.Ratings)
			.Where(m => rawIds.Contains(m.MediaId))
			.ToDictionaryAsync(m => m.MediaId, cancellationToken);

		var processed = new HashSet<Guid>();
		foreach (var item in items)
		{
			var raw = item.MediaId.Value;
			if (!processed.Add(raw))
			{
				continue;
			}

			if (existingByMediaId.TryGetValue(raw, out var existing))
			{
				metadataFactory.Update(existing, item.Details);
			}
			else
			{
				ctx.MediaMetadata.Add(metadataFactory.BuildFrom<MediaMetadataEntity>(item.Details, raw));
			}
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveManyAsync(IReadOnlyList<MediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		if (mediaIds.Count == 0)
		{
			return;
		}

		var rawIds = mediaIds.Select(id => id.Value).ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.MediaMetadata.Where(m => rawIds.Contains(m.MediaId)).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.MediaRatings.Where(r => r.Source == source).ExecuteDeleteAsync(cancellationToken);
	}
}
