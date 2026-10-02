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

	public Task<Result<MediaLookup>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default) =>
		id.Require<TmdbId>("TMDB enrichment requires a TMDB id.")
			.Map(tmdbId => tmdbApi.GetDetailAsync(tmdbId, cancellationToken).Map(details => ToLookup(details, tmdbId)));

	private MediaLookup ToLookup(TmdbTypes.Details details, TmdbId tmdbId) =>
		tmdbId.Type switch
		{
			MediaType.Movie => IdentifiedMedia.From(MapMovie(details, tmdbId)),
			MediaType.Show => IdentifiedMedia.From(MapShow(details, tmdbId)),
			MediaType.Unknown => throw new UnreachableException(),
		};

	private PartialMovie MapMovie(TmdbTypes.Details details, TmdbId sourceId) =>
		new()
		{
			Identity = new MediaIdentity(default, [sourceId]),
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

	private PartialShow MapShow(TmdbTypes.Details details, TmdbId sourceId) =>
		new()
		{
			Identity = new MediaIdentity(default, [sourceId]),
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

	private static RatingSummary BuildRating(TmdbTypes.Details details) =>
		details.VoteAverage is { } vote && vote > 0
			? RatingSummary.From([new MediaRating(MediaProvider.Tmdb, (float)vote, details.VoteCount)])
			: RatingSummary.None;

	private static DateOnly? ParseReleaseDate(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : null;
}
