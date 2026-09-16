using Contracts.Media.Metadata;
using Domain.Media;
using Domain.Providers;

namespace Contracts.Media;

public interface IMediaRepository
{
	Task<IReadOnlyList<MediaIdentity?>> FindIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<MediaIdentity>> UpsertIdentitiesAsync(IReadOnlyList<MediaIdentity> identities, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CachedMedia>> GetAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CachedMedia>> GetByExternalsAsync(IReadOnlyList<IExternalMediaId> candidates, CancellationToken cancellationToken = default);

	Task UpsertMetadataAsync(IReadOnlyList<IdentifiedMedia> media, CancellationToken cancellationToken = default);

	Task RemoveMetadataAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default);

	Task RemoveRatingsBySourceAsync(RatingProvider source, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider source,
		CancellationToken cancellationToken = default
	);
}
