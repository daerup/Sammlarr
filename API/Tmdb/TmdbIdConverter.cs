using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.Tmdb;

internal class TmdbIdConverter(PlexApi plexApi) : IExternalIdConverter
{
	public Task<Optional<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => candidates.FirstSomeAsync(TryConvertAsync);

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> TryConvertAsync(IExternalMediaId id) =>
		id switch
		{
			TmdbId tmdbId => Some.Of<IReadOnlyList<IExternalMediaId>>([tmdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, e => e is TmdbId),
			_ => new None(),
		};
}
