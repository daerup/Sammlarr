using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataRepository
{
	Task<CachedMediaMetadata?> GetByMediaIdAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default);

	Task RemoveManyAsync(IReadOnlyList<MediaId> mediaIds, CancellationToken cancellationToken = default);

	Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default);
}

public record CachedMediaMetadata(IMediaDetailResult Details, DateTimeOffset FetchedAt);

public record MediaMetadataUpsert(MediaId MediaId, IMediaDetailResult Details);
