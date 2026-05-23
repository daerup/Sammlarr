namespace Contracts.Media;

public interface IMediaRepository
{
	Task<List<IMediaId>> GetByIdsAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default);

	Task CreateAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default);

	Task UpsertAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default);
}
