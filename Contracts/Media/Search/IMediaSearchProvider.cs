using Domain.Media;

namespace Contracts.Media.Search;

public interface IMediaSearchProvider
{
	Task<IReadOnlyList<FoundMedia>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}
