using API.Plex;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.Tmdb;

internal class TmdbIdConverter(PlexApi plexApi) : IExternalIdConverter
{
	public Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => candidates.FirstSomeAsync(TryConvertAsync).ToResult("No TMDB id could be resolved for the given candidates.");

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> TryConvertAsync(IExternalMediaId id) =>
		id switch
		{
			TmdbId tmdbId => Some.Of<IReadOnlyList<IExternalMediaId>>([tmdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, e => e is TmdbId),
			_ => new None(),
		};
}
