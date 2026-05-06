using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public interface IMediaMetadataProvider
{
	Task<IMediaDetailResult> GetAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task ReportStaleAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
