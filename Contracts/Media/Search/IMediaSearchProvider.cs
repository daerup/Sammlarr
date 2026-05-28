namespace Contracts.Media.Search;

public interface IMediaSearchProvider
{
	Task<IReadOnlyList<IMediaDetail>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);

	Task<IMediaDetail> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default);
}
