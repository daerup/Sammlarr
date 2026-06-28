namespace Contracts.Media.Metadata;

public interface IMediaMetadataRepository
{
	Task<IReadOnlyList<CachedMedia>> QueryMediaMetadataAsync(
		IMediaQuerySession session,
		IQueryable<Guid> mediaIds,
		CancellationToken cancellationToken = default
	);

	Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default);

	Task RemoveManyAsync(IReadOnlyList<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default);
}

public record MediaMetadataUpsert(MediaId MediaId, MediaMatch Media);
