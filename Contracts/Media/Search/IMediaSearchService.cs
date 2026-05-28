namespace Contracts.Media.Search;

public interface IMediaSearchService
{
	Task<IReadOnlyList<IMediaDetail>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);
}
