using System.Globalization;
using API.Plex;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Tmdb;

internal class TmdbIdConverter(PlexApi plexApi, TmdbApi tmdbApi) : IExternalIdConverter
{
	private static readonly ById ByTmdbId = new(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);

	private static readonly ById Find = new(Accepts: [MediaProvider.Imdb, MediaProvider.Tvdb], Yields: [MediaProvider.Tmdb], Cost: 1);

	public MediaProvider Provider => MediaProvider.Tmdb;

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByTmdbId, Find];

	public IReadOnlyList<IdRoute> Routes { get; } =
	[
		new(Some.Of(MediaProvider.Tmdb), MediaProvider.Tmdb, [], 0),
		new(Some.Of(MediaProvider.Plex), MediaProvider.Tmdb, [MediaProvider.Plex], 1),
		new(Some.Of(MediaProvider.Imdb), MediaProvider.Tmdb, [MediaProvider.Tmdb], 1),
		new(Some.Of(MediaProvider.Tvdb), MediaProvider.Tmdb, [MediaProvider.Tmdb], 1),
	];

	public Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IdRoute route,
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => route.ConvertEachAsync(candidates, candidate => ConvertAsync(candidate, cancellationToken), "No TMDB id could be resolved for the given candidates.");

	private async Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(IExternalMediaId id, CancellationToken cancellationToken) =>
		id switch
		{
			TmdbId tmdbId => Success.Of<IReadOnlyList<IExternalMediaId>>([tmdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, external => external is TmdbId, cancellationToken),
			_ => await tmdbApi
				.FindAsync(id, cancellationToken)
				.Map(item => (IReadOnlyList<IExternalMediaId>)[TmdbId.From(item.Id.ToString(CultureInfo.InvariantCulture), id.Type)]),
		};
}
