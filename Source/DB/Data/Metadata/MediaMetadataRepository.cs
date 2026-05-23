using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Metadata;

internal class MediaMetadataRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	IMediaRepository mediaRepository,
	MediaMetadataFactory metadataFactory
) : IMediaMetadataRepository
{
	public async Task<CachedMediaMetadata?> GetByMediaIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx
			.MediaMetadata.AsNoTracking()
			.Include(m => m.Media)
				.ThenInclude(media => media.ExternalIds)
			.Include(m => m.Ratings)
			.SingleOrDefaultAsync(
				m =>
					m.Media.ExternalIds.Any(externalId =>
						externalId.Source == mediaId.Source && externalId.MediaType == mediaId.Type && externalId.Id == mediaId.Value
					),
				cancellationToken
			);

		if (entity is null)
		{
			return null;
		}

		var additionalIds = entity
			.Media.ExternalIds.Where(externalId =>
				externalId.Source != mediaId.Source || externalId.MediaType != mediaId.Type || externalId.Id != mediaId.Value
			)
			.Select(externalId => externalId.ToMediaId())
			.ToArray();

		return new CachedMediaMetadata(metadataFactory.ToDetailResult(entity, mediaId, additionalIds), entity.LastFetchedAt);
	}

	public async Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default)
	{
		if (items.Count == 0)
		{
			return;
		}

		foreach (var item in items)
		{
			await mediaRepository.UpsertAsync(item.Details.AdditionalIds.Prepend(item.Details.SourceId), cancellationToken);
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);

		var mediaIds = items.Select(item => item.MediaId).ToList();
		var rows = await ctx
			.ExternalMediaIds.WhereMatchingAny(mediaIds)
			.Select(externalId => new
			{
				externalId.Source,
				externalId.MediaType,
				externalId.Id,
				MediaId = externalId.Media.Id,
			})
			.ToListAsync(cancellationToken);
		var mediaIdByKey = rows.ToDictionary(r => (r.Source, r.MediaType, r.Id), r => r.MediaId);

		var mediaIdValues = mediaIdByKey.Values.Distinct().ToList();
		var existingByMediaId = await ctx
			.MediaMetadata.Include(m => m.Ratings)
			.Where(m => mediaIdValues.Contains(m.MediaId))
			.ToDictionaryAsync(m => m.MediaId, cancellationToken);

		var processedMediaIds = new HashSet<Guid>();
		foreach (var item in items)
		{
			var mediaIdValue = mediaIdByKey[(item.MediaId.Source, item.MediaId.Type, item.MediaId.Value)];
			if (!processedMediaIds.Add(mediaIdValue))
			{
				continue;
			}

			if (existingByMediaId.TryGetValue(mediaIdValue, out var existing))
			{
				metadataFactory.Update(existing, item.Details);
			}
			else
			{
				ctx.MediaMetadata.Add(metadataFactory.BuildFrom<MediaMetadataEntity>(item.Details, mediaIdValue));
			}
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveManyAsync(IReadOnlyList<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		if (mediaIds.Count == 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.MediaMetadata.Where(m => ctx.MediaMatchingAny(mediaIds).Any(media => media.Id == m.MediaId)).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.MediaRatings.Where(r => r.Source == source).ExecuteDeleteAsync(cancellationToken);
	}
}
