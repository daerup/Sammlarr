namespace Contracts.Media.Search;

public interface IMediaSearchService
{
	Task<IReadOnlyList<IMediaDetailResult>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);

	Task<IMediaDetailResult> LookUpAsync(IMediaId mediaId, CancellationToken cancellationToken = default);
}
