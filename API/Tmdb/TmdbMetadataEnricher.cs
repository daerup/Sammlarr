using System.Diagnostics;
using System.Globalization;
using API.Tmdb.Types;
using Contracts.Media.Metadata;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace API.Tmdb;

internal class TmdbMetadataEnricher(TmdbApi tmdbApi, TmdbArtwork artwork) : IMetadataEnricher
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
		| MediaField.Rating;

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
			Title = details.Title.NullIfBlank(),
			Tagline = details.Tagline.NullIfBlank(),
			Summary = details.Overview.NullIfBlank(),
			PosterUrl = artwork.Original(details.PosterPath).NullIfBlank(),
			BackdropUrl = artwork.Original(details.BackdropPath).NullIfBlank(),
			ReleaseDate = ParseReleaseDate(details.ReleaseDate ?? details.FirstAirDate),
			Genres = [.. (details.Genres ?? []).Select(g => g.Name).Where(name => !string.IsNullOrWhiteSpace(name))],
			Rating = BuildRating(details),
			Runtime = details.Runtime is > 0 ? TimeSpan.FromMinutes(details.Runtime.Value) : null,
		};
	}

	private ShowDetails MapShow(TmdbTypes.Details details)
	{
		return new()
		{
			Title = details.Name.NullIfBlank(),
			Tagline = details.Tagline.NullIfBlank(),
			Summary = details.Overview.NullIfBlank(),
			PosterUrl = artwork.Original(details.PosterPath).NullIfBlank(),
			BackdropUrl = artwork.Original(details.BackdropPath).NullIfBlank(),
			ReleaseDate = ParseReleaseDate(details.FirstAirDate ?? details.ReleaseDate),
			Genres = [.. (details.Genres ?? []).Select(g => g.Name).Where(name => !string.IsNullOrWhiteSpace(name))],
			Rating = BuildRating(details),
			Seasons = details.NumberOfSeasons,
			Episodes = details.NumberOfEpisodes,
			RuntimePerEpisode = details.EpisodeRunTime is { Count: > 0 } runtimes && runtimes[0] > 0 ? TimeSpan.FromMinutes(runtimes[0]) : null,
		};
	}

	private static RatingSummary BuildRating(TmdbTypes.Details details) =>
		details.VoteAverage is { } vote && vote > 0
			? RatingSummary.From([new MediaRating(MediaProvider.Tmdb, (float)vote, details.VoteCount)])
			: RatingSummary.None;

	private static DateOnly? ParseReleaseDate(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : null;
}
