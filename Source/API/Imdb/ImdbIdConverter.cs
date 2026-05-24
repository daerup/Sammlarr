using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.IMDB;

internal class ImdbIdConverter(PlexApi plexApi)
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
			ImdbId imdbId => Some.Of<IReadOnlyList<IExternalMediaId>>([imdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, e => e is ImdbId),
			_ => new None(),
		};
}
