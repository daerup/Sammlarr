using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataRepository
{
	Task<CachedMediaMetadata?> GetByMediaIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task UpsertAsync(IMediaId mediaId, IMediaDetailResult details, CancellationToken cancellationToken = default);

	Task UpsertManyAsync(IReadOnlyList<MediaMetadataUpsert> items, CancellationToken cancellationToken = default);

	Task RemoveAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task RemoveRatingsBySourceAsync(RatingSource source, CancellationToken cancellationToken = default);
}

public record CachedMediaMetadata(IMediaDetailResult Details, DateTimeOffset FetchedAt);

public record MediaMetadataUpsert(IMediaId MediaId, IMediaDetailResult Details);
