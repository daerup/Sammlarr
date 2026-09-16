using API.Plex;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.Imdb;

internal class ImdbIdConverter(PlexApi plexApi) : IExternalIdConverter
{
	public Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => candidates.FirstSomeAsync(TryConvertAsync).ToResult("No IMDb id could be resolved for the given candidates.");

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> TryConvertAsync(IExternalMediaId id) =>
		id switch
		{
			ImdbId imdbId => Some.Of<IReadOnlyList<IExternalMediaId>>([imdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, e => e is ImdbId),
			_ => new None(),
		};
}
