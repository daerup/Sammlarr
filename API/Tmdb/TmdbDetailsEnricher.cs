using System.Diagnostics;
using API.Tmdb.Types;
using Contracts.Media;
using Domain.Media;

namespace API.Tmdb;

internal class TmdbDetailsEnricher(TmdbApi tmdbApi, TmdbArtwork artwork) : IDetailsEnricher
{
	public MediaField Fills =>
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
		| MediaField.TmdbRating;

	public Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return id.Require<TmdbId>("TMDB enrichment requires a TMDB id.")
			.Map(tmdbId => tmdbApi.GetDetailAsync(tmdbId, cancellationToken).Map(details => ToFound(details, tmdbId)));
	}

	private FoundMedia ToFound(TmdbTypes.Details details, TmdbId tmdbId)
	{
		MediaDetails mapped = tmdbId.Type switch
		{
			MediaType.Movie => MapMovie(details),
			MediaType.Show => MapShow(details),
			MediaType.Unknown => throw new UnreachableException(),
		};
		return new([tmdbId], mapped);
	}

	private MovieDetails MapMovie(TmdbTypes.Details details)
	{
		return new()
		{
			Title = ProviderValues.Text(details.Title),
			Tagline = ProviderValues.Text(details.Tagline),
			Summary = ProviderValues.Text(details.Overview),
			PosterUrl = artwork.Original(details.PosterPath),
			BackdropUrl = artwork.Original(details.BackdropPath),
			ReleaseDate = details.ReleasedAs(MediaType.Movie),
			Genres = ProviderValues.Genres(details.Genres?.Select(genre => genre.Name)),
			Rating = TmdbRatings.Of(details.VoteAverage, details.VoteCount),
			Runtime = ProviderValues.Minutes(details.Runtime),
		};
	}

	private ShowDetails MapShow(TmdbTypes.Details details)
	{
		return new()
		{
			Title = ProviderValues.Text(details.Name),
			Tagline = ProviderValues.Text(details.Tagline),
			Summary = ProviderValues.Text(details.Overview),
			PosterUrl = artwork.Original(details.PosterPath),
			BackdropUrl = artwork.Original(details.BackdropPath),
			ReleaseDate = details.ReleasedAs(MediaType.Show),
			Genres = ProviderValues.Genres(details.Genres?.Select(genre => genre.Name)),
			Rating = TmdbRatings.Of(details.VoteAverage, details.VoteCount),
			Seasons = ProviderValues.Count(details.NumberOfSeasons),
			Episodes = ProviderValues.Count(details.NumberOfEpisodes),
			RuntimePerEpisode = ProviderValues.Minutes(details.EpisodeRunTime?.FirstOrDefault()),
		};
	}
}
