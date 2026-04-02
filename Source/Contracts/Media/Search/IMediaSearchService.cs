namespace Contracts.Media.Search;

public interface IMediaSearchService
{
	Task<IReadOnlyList<IMediaSearchResult>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}
