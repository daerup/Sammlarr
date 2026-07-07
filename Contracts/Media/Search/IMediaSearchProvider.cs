using Domain.Media;

namespace Contracts.Media.Search;

public interface IMediaSearchProvider
{
	Task<IReadOnlyList<MediaLookup>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);

	Task<Result<MediaLookup>> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default);
}
