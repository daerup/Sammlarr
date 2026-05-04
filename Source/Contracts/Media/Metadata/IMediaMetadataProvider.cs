using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataProvider
{
	Task<IMediaDetailResult> GetAsync(IMediaId anchor, CancellationToken cancellationToken = default);

	Task ReportStaleAsync(IMediaId anchor, CancellationToken cancellationToken = default);
}
