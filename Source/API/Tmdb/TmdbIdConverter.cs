using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.TMDB;

internal class TmdbIdConverter(PlexApi plexApi)
{
	public async Task<Optional<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		foreach (var id in candidates)
		{
			var result = await TryConvertAsync(id);
			if (result.Value is Some<IReadOnlyList<IExternalMediaId>>)
			{
				return result;
			}
		}

		return new None();
	}

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> TryConvertAsync(IExternalMediaId id) =>
		id switch
		{
			TmdbId tmdbId => Some.Of<IReadOnlyList<IExternalMediaId>>([tmdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, e => e is TmdbId),
			_ => new None(),
		};
}
