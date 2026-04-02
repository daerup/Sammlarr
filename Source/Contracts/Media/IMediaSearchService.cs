namespace Contracts.Media;

public interface IMediaSearchService
{
	Task<IReadOnlyList<MediaSearchResult>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}
