using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataProvider
{
	IAsyncEnumerable<IMediaDetailResult> GetAsync(IReadOnlyList<IMediaId> mediaIds, CancellationToken cancellationToken = default);

	Task InvalidateAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default);
}
