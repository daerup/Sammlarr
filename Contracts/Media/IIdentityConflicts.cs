using Domain.Media;

namespace Contracts.Media;

public interface IIdentityConflicts
{
	Task<IReadOnlyList<IdentityConflict>> DueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<IdentityConflict>> AllAsync(CancellationToken cancellationToken = default);

	Task SaveAsync(IdentityConflict conflict, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, TitleUses>> UsesAsync(IReadOnlyCollection<MediaId> titles, CancellationToken cancellationToken = default);
}
