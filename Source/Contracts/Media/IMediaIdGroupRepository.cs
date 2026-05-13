namespace Contracts.Media;

public interface IMediaIdGroupRepository
{
	Task<List<IMediaId>> GetByIdsAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default);

	Task CreateAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default);

	Task UpsertAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default);

	Task AppendAsync(IMediaId existingId, IEnumerable<IMediaId> newIds, CancellationToken cancellationToken = default);
}
