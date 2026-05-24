namespace Contracts.Media;

public interface IMediaRepository
{
	Task<IReadOnlyList<MediaIdentity?>> FindManyAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<MediaIdentity>> UpsertManyAsync(IReadOnlyList<MediaIdentity> identities, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider source,
		CancellationToken cancellationToken = default
	);

	IQueryable<Guid> QueryMediaIds(IMediaQuerySession session, IEnumerable<IExternalMediaId> candidates);

	IQueryable<Guid> QueryMediaIds(IMediaQuerySession session, IEnumerable<MediaId> ids);
}
