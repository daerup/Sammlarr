namespace Contracts.Media;

public interface IMediaIdGroupStore
{
	Task<List<IMediaId>> GetByIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default);

	Task CreateAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default);

	Task CreateOrAppendAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default);

	Task AppendAsync(IMediaId existingId, IEnumerable<IMediaId> newIds, CancellationToken cancellationToken = default);
}
