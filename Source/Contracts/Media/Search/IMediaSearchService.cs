namespace Contracts.Media.Search;

public interface IMediaSearchService
{
	Task<IReadOnlyList<IMediaDetailResult>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);

	Task<IMediaDetailResult> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default);
}
