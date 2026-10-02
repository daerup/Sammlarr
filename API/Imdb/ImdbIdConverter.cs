using API.Plex;
using API.Tmdb;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Imdb;

internal class ImdbIdConverter(PlexApi plexApi, TmdbApi tmdbApi) : IExternalIdConverter
{
	public MediaProvider Provider => MediaProvider.Imdb;

	public IReadOnlyList<IdLookup> Lookups { get; } = [];

	public IReadOnlyList<IdRoute> Routes { get; } =
	[
		new(Some.Of(MediaProvider.Imdb), MediaProvider.Imdb, [], 0),
		new(Some.Of(MediaProvider.Plex), MediaProvider.Imdb, [MediaProvider.Plex], 1),
		new(Some.Of(MediaProvider.Tmdb), MediaProvider.Imdb, [MediaProvider.Tmdb], 1),
	];

	public Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IdRoute route,
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	) => route.ConvertEachAsync(candidates, candidate => ConvertAsync(candidate, cancellationToken), "No IMDb id could be resolved for the given candidates.");

	private async Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(IExternalMediaId id, CancellationToken cancellationToken) =>
		id switch
		{
			ImdbId imdbId => Success.Of<IReadOnlyList<IExternalMediaId>>([imdbId]),
			PlexId plexId => await plexApi.ResolveExternalsByPlexAsync(plexId, external => external is ImdbId, cancellationToken),
			TmdbId tmdbId => await tmdbApi
				.GetExternalIdsAsync(tmdbId, cancellationToken)
				.When(external => !string.IsNullOrWhiteSpace(external.ImdbId), $"TMDB knows no IMDb id for {tmdbId.Value}.")
				.Map(external => (IReadOnlyList<IExternalMediaId>)[ImdbId.From(external.ImdbId!, tmdbId.Type)]),
			_ => Error.From($"No IMDb route starts from a {id.Source} id."),
		};
}
