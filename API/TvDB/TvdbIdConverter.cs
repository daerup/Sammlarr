using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.TvDB;

internal class TvdbIdConverter(PlexApi plexApi) : IExternalIdConverter
{
	public Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => candidates.FirstSomeAsync(TryConvertAsync).ToResult("No TheTVDB id could be resolved for the given candidates.");

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> TryConvertAsync(IExternalMediaId id) =>
		id switch
		{
			TvdbId tvdbId => Some.Of<IReadOnlyList<IExternalMediaId>>([tvdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, e => e is TvdbId),
			_ => new None(),
		};
}
