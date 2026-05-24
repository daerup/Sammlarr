using System.Globalization;
using API.Tmdb.Types;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Shared.Options;

namespace API.Tmdb;

internal class TmdbMetadataEnricher(TmdbApi tmdbApi, TmdbOptions options) : IMetadataEnricher
{
	public async Task<IMediaDetail?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		if (id is not TmdbId tmdbId)
		{
			return null;
		}

		try
		{
			var details = await tmdbApi.GetDetailAsync(tmdbId, cancellationToken);
			if (details is null)
			{
				return null;
			}
			return tmdbId.Type switch
			{
				MediaType.Movie => MapMovie(details, tmdbId),
				MediaType.Show => MapShow(details, tmdbId),
				_ => null,
			};
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	private MovieDetail MapMovie(TmdbTypes.Details details, TmdbId sourceId) =>
		new()
		{
			Identity = new MediaIdentity(default, [sourceId]),
			Title = details.Title ?? string.Empty,
			Tagline = details.Tagline ?? string.Empty,
			Summary = details.Overview ?? string.Empty,
			PosterUrl = ComposeImageUrl(details.PosterPath),
			BackdropUrl = ComposeImageUrl(details.BackdropPath),
			ReleaseDate = ParseReleaseDate(details.ReleaseDate ?? details.FirstAirDate),
			Genres = details.Genres?.Select(g => g.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [],
			Runtime = details.Runtime is > 0 ? TimeSpan.FromMinutes(details.Runtime.Value) : TimeSpan.Zero,
		};

	private ShowDetail MapShow(TmdbTypes.Details details, TmdbId sourceId) =>
		new()
		{
			Identity = new MediaIdentity(default, [sourceId]),
			Title = details.Name ?? string.Empty,
			Tagline = details.Tagline ?? string.Empty,
			Summary = details.Overview ?? string.Empty,
			PosterUrl = ComposeImageUrl(details.PosterPath),
			BackdropUrl = ComposeImageUrl(details.BackdropPath),
			ReleaseDate = ParseReleaseDate(details.FirstAirDate ?? details.ReleaseDate),
			Genres = details.Genres?.Select(g => g.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [],
			Seasons = details.NumberOfSeasons ?? 0,
			Episodes = details.NumberOfEpisodes ?? 0,
			RuntimePerEpisode = details.EpisodeRunTime is { Count: > 0 } runtimes && runtimes[0] > 0 ? TimeSpan.FromMinutes(runtimes[0]) : TimeSpan.Zero,
		};

	private string ComposeImageUrl(string? path) => string.IsNullOrWhiteSpace(path) ? string.Empty : options.ImageBaseUrl + path;

	private static DateOnly ParseReleaseDate(string? value) =>
		DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : DateOnly.MinValue;
}
