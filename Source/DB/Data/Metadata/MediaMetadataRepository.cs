using Contracts;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.MediaGroup;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Metadata;

internal class MediaMetadataRepository(SammlarrDbContext dbContext, IMediaIdGroupRepository mediaIdGroupRepository) : IMediaMetadataRepository
{
	public async Task<CachedMediaMetadata?> GetByMediaIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(mediaId);
		var entity = await dbContext
			.MediaMetadata.Include(m => m.MediaIdGroup)
				.ThenInclude(g => g.MediaIdEntries)
			.SingleOrDefaultAsync(m => m.MediaIdGroup.MediaIdEntries.Any(entry => entry.Identifier == identifier), cancellationToken);

		if (entity is null)
		{
			return null;
		}

		var additionalIds = entity
			.MediaIdGroup.MediaIdEntries.Select(entry => entry.Identifier.ToMediaId())
			.Where(id => !MediaIdentifier.From(id).Equals(identifier))
			.ToArray();

		return new CachedMediaMetadata(ToDetailResult(entity, mediaId, additionalIds), entity.UpdatedAt);
	}

	public async Task UpsertAsync(IMediaId anchor, IMediaDetailResult details, CancellationToken cancellationToken = default)
	{
		await mediaIdGroupRepository.CreateOrAppendAsync(details.AdditionalIds.Prepend(details.SourceId), cancellationToken);

		var anchorIdentifier = MediaIdentifier.From(anchor);
		var group = await dbContext
			.MediaIdEntries.Include(entry => entry.MediaIdGroupEntity)
			.Where(entry => entry.Identifier == anchorIdentifier)
			.Select(entry => entry.MediaIdGroupEntity)
			.SingleAsync(cancellationToken);

		var existing = await dbContext.MediaMetadata.SingleOrDefaultAsync(m => m.MediaIdGroupId == group.Id, cancellationToken);
		if (existing is not null)
		{
			dbContext.MediaMetadata.Remove(existing);
		}

		dbContext.MediaMetadata.Add(BuildEntity(details, group.Id));
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveAsync(IMediaId anchor, CancellationToken cancellationToken = default)
	{
		var identifier = MediaIdentifier.From(anchor);
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

	private static MediaMetadataEntity BuildEntity(IMediaDetailResult details, Guid groupId) =>
		details switch
		{
			MovieDetailResult movie => new MovieMetadataEntity
			{
				MediaIdGroupId = groupId,
				Title = movie.Title,
				Tagline = movie.Tagline,
				Summary = movie.Summary,
				PosterUrl = movie.PosterUrl,
				BackdropUrl = movie.BackdropUrl,
				ReleaseDate = movie.ReleaseDate,
				Runtime = movie.Runtime,
				Genres = [.. movie.Genres],
			},
			ShowDetailResult show => new ShowMetadataEntity
			{
				MediaIdGroupId = groupId,
				Title = show.Title,
				Tagline = show.Tagline,
				Summary = show.Summary,
				PosterUrl = show.PosterUrl,
				BackdropUrl = show.BackdropUrl,
				ReleaseDate = show.ReleaseDate,
				RuntimePerEpisode = show.RuntimePerEpisode,
				Seasons = show.Seasons,
				Episodes = show.Episodes,
				Genres = [.. show.Genres],
			},
			_ => throw new InvalidOperationException($"Unsupported media detail result type '{details.GetType().Name}'."),
		};

	private static IMediaDetailResult ToDetailResult(MediaMetadataEntity entity, IMediaId sourceId, IMediaId[] additionalIds) =>
		entity switch
		{
			MovieMetadataEntity movie => new MovieDetailResult
			{
				SourceId = sourceId,
				AdditionalIds = additionalIds,
				Title = movie.Title,
				Tagline = movie.Tagline,
				Summary = movie.Summary,
				PosterUrl = movie.PosterUrl,
				BackdropUrl = movie.BackdropUrl,
				Runtime = movie.Runtime,
				ReleaseDate = movie.ReleaseDate,
				Genres = movie.Genres,
			},
			ShowMetadataEntity show => new ShowDetailResult
			{
				SourceId = sourceId,
				AdditionalIds = additionalIds,
				Title = show.Title,
				Tagline = show.Tagline,
				Summary = show.Summary,
				PosterUrl = show.PosterUrl,
				BackdropUrl = show.BackdropUrl,
				RuntimePerEpisode = show.RuntimePerEpisode,
				ReleaseDate = show.ReleaseDate,
				Genres = show.Genres,
				Seasons = show.Seasons,
				Episodes = show.Episodes,
			},
			_ => throw new InvalidOperationException($"Unsupported metadata entity type '{entity.GetType().Name}'."),
		};
}
