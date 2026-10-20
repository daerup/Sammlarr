using Domain.Media;

namespace Contracts.Media;

public interface IShowParts
{
	Task<IReadOnlyDictionary<IExternalMediaId, Optional<IReadOnlyList<IExternalMediaId>>>> GetShowsAsync(
		IReadOnlyCollection<IExternalMediaId> parts,
		CancellationToken cancellationToken = default
	);

	Task AddAsync(IReadOnlyCollection<IExternalMediaId> parts, Optional<MediaId> show, ShowPartPlace place, CancellationToken cancellationToken = default);
}

public sealed record ShowPartPlace(Optional<int> TvdbSeason, Optional<int> EpisodeOffset);
