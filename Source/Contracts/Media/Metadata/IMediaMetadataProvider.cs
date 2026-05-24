using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataProvider
{
	Task<IMediaDetailResult?> GetAsync(MediaId mediaId, CancellationToken cancellationToken = default);

	Task InvalidateAsync(IEnumerable<MediaId> mediaIds, CancellationToken cancellationToken = default);
}
