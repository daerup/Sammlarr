namespace Contracts.Media;

public interface IMediaRepository
{
	Task<MediaIdentity?> FindAsync(IEnumerable<IExternalMediaId> externals, CancellationToken cancellationToken = default);

	Task<MediaIdentity?> FindAsync(MediaId id, CancellationToken cancellationToken = default);

	Task<MediaIdentity> UpsertAsync(IEnumerable<IExternalMediaId> externals, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider source,
		CancellationToken cancellationToken = default
	);
}
