using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Metadata;

internal class MediaMetadataRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, MediaMetadataFactory metadataFactory) : IMediaMetadataRepository
{
	public async Task<IReadOnlyList<CachedMedia>> QueryMediaMetadataAsync(
		IMediaQuerySession session,
		IQueryable<Guid> mediaIds,
		CancellationToken cancellationToken = default
	)
	{
		var ctx = ((MediaQuerySession)session).Context;

		var query =
			from m in ctx.Media.AsNoTracking().Include(m => m.ExternalIds)
			where mediaIds.Contains(m.Id)
			join md in ctx.MediaMetadata.AsNoTracking().Include(x => x.Ratings) on m.Id equals md.MediaId into joined
			from md in joined.DefaultIfEmpty()
			select new { Media = m, Metadata = md };

		var rows = await query.AsSplitQuery().ToListAsync(cancellationToken);
		return rows.Select(row =>
			{
				var mediaId = MediaId.From(row.Media.Id);
				var externals = row.Media.ExternalIds.Select(e => e.ToExternalMediaId()).ToList();
				var identity = new MediaIdentity(mediaId, externals);
				return row.Metadata is null
					? new CachedMedia(Unresolved(identity), null)
					: new CachedMedia(metadataFactory.ToDetailResult(row.Metadata, identity), row.Metadata.LastFetchedAt);
			})
			.ToList();
	}

	private static UnresolvedMedia Unresolved(MediaIdentity identity) =>
		new()
		{
			Identity = identity,
			MediaType = MediaType.Unknown,
			Title = string.Empty,
			ReleaseDate = DateOnly.MinValue,
		};

	public async Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default)
	{
		if (items.Count == 0)
		{
			return;
		}

		var deduped = items.GroupBy(i => i.MediaId.Value).Select(g => g.Last()).ToList();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rawIds = deduped.Select(i => i.MediaId.Value).ToList();
		var existing = await ctx.MediaMetadata.Include(m => m.Ratings).Where(m => rawIds.Contains(m.MediaId)).ToDictionaryAsync(m => m.MediaId, cancellationToken);

		foreach (var item in deduped)
		{
			if (existing.TryGetValue(item.MediaId.Value, out var entity))
			{
				metadataFactory.Update(entity, item.Details);
			}
			else
			{
				ctx.MediaMetadata.Add(metadataFactory.BuildFrom<MediaMetadataEntity>(item.Details, item.MediaId.Value));
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
