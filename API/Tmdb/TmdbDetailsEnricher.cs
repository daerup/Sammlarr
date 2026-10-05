using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Tmdb;

internal class TmdbDetailsEnricher(TmdbApi tmdbApi, TmdbArtwork artwork) : IDetailsEnricher
{
	public MediaFields Fills { get; } =
		MediaField.Title
		| MediaField.Tagline
		| MediaField.Summary
		| MediaField.Poster
		| MediaField.Backdrop
		| MediaField.ReleaseDate
		| MediaField.Genres
		| MediaField.Runtime
		| MediaField.Seasons
		| MediaField.Episodes
		| MediaField.RuntimePerEpisode
		| MediaFields.Rated(MediaProvider.Tmdb.SuppliedRatingSources());

	public Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return id.Require<TmdbId>("TMDB enrichment requires a TMDB id.")
			.Map(tmdbId => tmdbApi.GetDetailAsync(tmdbId, cancellationToken).Map(details => TmdbFound.Of(details, tmdbId, artwork)));
	}
}
