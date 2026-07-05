using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.Imdb;

internal class ImdbIdConverter(PlexApi plexApi) : IExternalIdConverter
{
	public Task<Optional<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => candidates.FirstSomeAsync(TryConvertAsync);

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> TryConvertAsync(IExternalMediaId id) =>
		id switch
		{
			ImdbId imdbId => Some.Of<IReadOnlyList<IExternalMediaId>>([imdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, e => e is ImdbId),
			_ => new None(),
		};
}
