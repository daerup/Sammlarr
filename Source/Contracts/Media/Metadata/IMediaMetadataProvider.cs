using System.Diagnostics;
using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataProvider
{
	Task<IMediaDetailResult> GetAsync(IMediaId mediaId, CancellationToken cancellationToken = default, ActivityContext parent = default);

	Task InvalidateAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default);

	Task PurgeAllAsync(CancellationToken cancellationToken = default);
}
