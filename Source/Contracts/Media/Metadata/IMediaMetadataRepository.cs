using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataRepository
{
	Task<CachedMediaMetadata?> GetByMediaIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task UpsertAsync(IMediaId anchor, IMediaDetailResult details, CancellationToken cancellationToken = default);

	Task RemoveAsync(IMediaId anchor, CancellationToken cancellationToken = default);
}

public record CachedMediaMetadata(IMediaDetailResult Details, DateTimeOffset FetchedAt);
