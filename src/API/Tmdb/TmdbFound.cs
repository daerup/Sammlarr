using System.Diagnostics;
using API.Tmdb.Types;
using Domain.Media;

namespace API.Tmdb;

internal static class TmdbFound
{
	internal static FoundMedia Of(TmdbTypes.Trending.Result result, TmdbArtwork artwork)
	{
		return new FoundMedia(
			[result.TmdbId],
			MediaDetails.Empty(result.MediaType) with
			{
				Title = result.DisplayTitle,
				Summary = ProviderValues.Text(result.Overview),
				PosterUrl = artwork.Original(result.PosterPath),
				BackdropUrl = artwork.Original(result.BackdropPath),
				ReleaseDate = result.Released,
				Ratings = TmdbRatings.Of(result.VoteAverage, result.VoteCount),
			}
		);
	}

	internal static FoundMedia Of(TmdbTypes.Details details, TmdbId tmdbId, TmdbArtwork artwork)
	{
		MediaDetails mapped = tmdbId.Type switch
		{
			MediaType.Movie => Movie(details, artwork),
			MediaType.Show => Show(details, artwork),
			MediaType.Unknown => throw new UnreachableException(),
		};
		return new([tmdbId], mapped);
	}

	private static MovieDetails Movie(TmdbTypes.Details details, TmdbArtwork artwork)
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
			Ratings = TmdbRatings.Of(details.VoteAverage, details.VoteCount),
			Runtime = ProviderValues.Minutes(details.Runtime),
		};
	}

	private static ShowDetails Show(TmdbTypes.Details details, TmdbArtwork artwork)
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
			Ratings = TmdbRatings.Of(details.VoteAverage, details.VoteCount),
			Seasons = ProviderValues.Count(details.NumberOfSeasons),
			Episodes = ProviderValues.Count(details.NumberOfEpisodes),
			RuntimePerEpisode = ProviderValues.Minutes(details.EpisodeRunTime?.FirstOrDefault()),
		};
	}
}
