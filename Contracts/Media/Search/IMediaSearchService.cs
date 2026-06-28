namespace Contracts.Media.Search;

public interface IMediaSearchService
{
	Task<IReadOnlyList<MediaLookup>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}
