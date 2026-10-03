using Domain.Media;

namespace Contracts.Media.Search;

public interface IMediaSearchService
{
	Task<IReadOnlyList<MediaMatchResult>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}
