using System.Globalization;
using API.Plex;
using API.Tmdb;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.TvDB;

internal class TvdbIdConverter(PlexApi plexApi, TmdbApi tmdbApi) : IExternalIdConverter
{
	public MediaProvider Provider => MediaProvider.Tvdb;

	public IReadOnlyList<IdLookup> Lookups { get; } = [];

	public IReadOnlyList<IdRoute> Routes { get; } =
	[
		new(Some.Of(MediaProvider.Tvdb), MediaProvider.Tvdb, [], 0),
		new(Some.Of(MediaProvider.Plex), MediaProvider.Tvdb, [MediaProvider.Plex], 1),
		new(Some.Of(MediaProvider.Tmdb), MediaProvider.Tvdb, [MediaProvider.Tmdb], 1),
	];

	public Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IdRoute route,
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => route.ConvertEachAsync(candidates, candidate => ConvertAsync(candidate, cancellationToken), "No TheTVDB id could be resolved for the given candidates.");

	private async Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(IExternalMediaId id, CancellationToken cancellationToken) =>
		id switch
		{
			TvdbId tvdbId => Success.Of<IReadOnlyList<IExternalMediaId>>([tvdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, external => external is TvdbId, cancellationToken),
			TmdbId tmdbId => await tmdbApi
				.GetExternalIdsAsync(tmdbId, cancellationToken)
				.When(external => external.TvdbId is > 0, $"TMDB knows no TheTVDB id for {tmdbId.Value}.")
				.Map(external => (IReadOnlyList<IExternalMediaId>)[TvdbId.From(external.TvdbId!.Value.ToString(CultureInfo.InvariantCulture), tmdbId.Type)]),
			_ => Error.From($"No TheTVDB route starts from a {id.Source} id."),
		};
}
