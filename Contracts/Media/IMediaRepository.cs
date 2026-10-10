using Domain.Media;
using Domain.Providers;

namespace Contracts.Media;

public interface IMediaRepository
{
	Task<IReadOnlyList<Optional<MediaIdentity>>> FindIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<MediaIdentity>> UpsertIdentitiesAsync(IReadOnlyList<MediaIdentity> identities, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<StoredMedia>> GetAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<StoredMedia>> GetByExternalsAsync(IReadOnlyList<IExternalMediaId> candidates, CancellationToken cancellationToken = default);

	Task UpsertDetailsAsync(IReadOnlyDictionary<MediaId, CachedDetails> details, CancellationToken cancellationToken = default);

	Task SavePageLinksAsync(MediaId id, IReadOnlyCollection<IPageLink> links, CancellationToken cancellationToken = default);

	Task RemoveDetailsAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider source,
		CancellationToken cancellationToken = default
	);
}
