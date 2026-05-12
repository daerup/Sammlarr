using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Metadata;

internal class MediaMetadataRepository(SammlarrDbContext dbContext, IMediaIdGroupRepository mediaIdGroupRepository, MediaMetadataFactory metadataFactory)
	: IMediaMetadataRepository
{
	public async Task<CachedMediaMetadata?> GetByMediaIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		var entity = await dbContext
			.MediaMetadata.AsNoTracking()
			.Include(m => m.MediaIdGroup)
				.ThenInclude(g => g.MediaIdEntries)
			.Include(m => m.Ratings)
			.SingleOrDefaultAsync(m => m.MediaIdGroup.MediaIdEntries.Any(entry => entry.Identifier == identifier), cancellationToken);

		if (entity is null)
		{
			return null;
		}

		var additionalIds = entity
			.MediaIdGroup.MediaIdEntries.Select(entry => entry.Identifier.ToMediaId())
			.Where(id => !MediaIdentifier.From(id).Equals(identifier))
			.ToArray();

		return new CachedMediaMetadata(metadataFactory.ToDetailResult(entity, mediaId, additionalIds), entity.UpdatedAt);
	}

	public Task UpsertAsync(IMediaId mediaId, IMediaDetailResult details, CancellationToken cancellationToken = default) =>
		UpsertManyAsync([new MediaMetadataUpsert(mediaId, details)], cancellationToken);

	public async Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default)
	{
		if (items.Count == 0)
		{
			return;
		}

		foreach (var item in items)
		{
			await mediaIdGroupRepository.CreateOrAppendAsync(item.Details.AdditionalIds.Prepend(item.Details.SourceId), cancellationToken);
		}

		var identifiers = items.Select(item => MediaIdentifier.From(item.MediaId)).ToHashSet();
		var groupIdByIdentifier = await dbContext
			.MediaIdEntries.Where(MediaIdentifierGroupEntryExpressions.MatchesAnyIdentifier(identifiers))
			.Select(entry => new { entry.Identifier, GroupId = entry.MediaIdGroupEntity.Id })
			.ToDictionaryAsync(x => x.Identifier, x => x.GroupId, cancellationToken);

		var groupIds = groupIdByIdentifier.Values.Distinct().ToList();
		var existingByGroupId = await dbContext
			.MediaMetadata.Include(m => m.Ratings)
			.Where(m => groupIds.Contains(m.MediaIdGroupId))
			.ToDictionaryAsync(m => m.MediaIdGroupId, cancellationToken);

		// Clean up any orphaned ratings for groups whose MediaMetadata was removed out-of-band (e.g. manual SQL delete bypassing the cascade).
		var orphanGroupIds = groupIds.Where(id => !existingByGroupId.ContainsKey(id)).ToList();
		if (orphanGroupIds.Count > 0)
		{
			await dbContext.MediaRatings.Where(r => orphanGroupIds.Contains(EF.Property<Guid>(r, "MediaIdGroupId"))).ExecuteDeleteAsync(cancellationToken);
		}

		var processedGroupIds = new HashSet<Guid>();
		foreach (var item in items)
		{
			var identifier = MediaIdentifier.From(item.MediaId);
			var groupId = groupIdByIdentifier[identifier];
			if (!processedGroupIds.Add(groupId))
			{
				continue;
			}

			if (existingByGroupId.TryGetValue(groupId, out var existing))
			{
				metadataFactory.Update(existing, item.Details);
			}
			else
			{
				dbContext.MediaMetadata.Add(metadataFactory.BuildFrom<MediaMetadataEntity>(item.Details, groupId));
			}
		}

		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		var entity = await dbContext
			.MediaMetadata.Include(m => m.MediaIdGroup)
				.ThenInclude(g => g.MediaIdEntries)
			.SingleOrDefaultAsync(m => m.MediaIdGroup.MediaIdEntries.Any(entry => entry.Identifier == identifier), cancellationToken);

		if (entity is null)
		{
			return;
		}

		dbContext.MediaMetadata.Remove(entity);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task PurgeAllAsync(CancellationToken cancellationToken = default)
	{
		await dbContext.MediaRatings.ExecuteDeleteAsync(cancellationToken);
		await dbContext.MediaMetadata.ExecuteDeleteAsync(cancellationToken);
		await dbContext.MediaIdEntries.ExecuteDeleteAsync(cancellationToken);
		await dbContext.MediaIdGroups.ExecuteDeleteAsync(cancellationToken);
	}

	public Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default) =>
		dbContext.MediaRatings.Where(r => r.Source == source).ExecuteDeleteAsync(cancellationToken);
}
