namespace Contracts.Media.Search;

public interface IMediaSearchProvider
{
	Task<IReadOnlyList<MediaMatch>> SearchAsync(string query, IReadOnlyCollection<MediaType> mediaTypes, CancellationToken cancellationToken = default);

	Task<MediaMatch> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default);
}
