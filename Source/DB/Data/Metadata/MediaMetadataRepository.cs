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
			.MediaMetadata.Include(m => m.MediaIdGroup)
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

	public async Task UpsertAsync(IMediaId mediaId, IMediaDetailResult details, CancellationToken cancellationToken = default)
	{
		await mediaIdGroupRepository.CreateOrAppendAsync(details.AdditionalIds.Prepend(details.SourceId), cancellationToken);

		var identifier = MediaIdentifier.From(mediaId);
		var group = await dbContext
			.MediaIdEntries.Include(entry => entry.MediaIdGroupEntity)
			.Where(entry => entry.Identifier == identifier)
			.Select(entry => entry.MediaIdGroupEntity)
			.SingleAsync(cancellationToken);

		var existing = await dbContext.MediaMetadata.Include(m => m.Ratings).SingleOrDefaultAsync(m => m.MediaIdGroupId == group.Id, cancellationToken);

		if (existing is null)
		{
			dbContext.MediaMetadata.Add(metadataFactory.BuildFrom<MediaMetadataEntity>(details, group.Id));
		}
		else
		{
			metadataFactory.Update(existing, details);
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

	public Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default) =>
		dbContext.MediaRatings.Where(r => r.Source == source).ExecuteDeleteAsync(cancellationToken);
}
