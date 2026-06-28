using System.Globalization;
using API.Tmdb.Types;
using Contracts.Media.Metadata;
using Shared.Options;

namespace API.Tmdb;

internal class TmdbMetadataEnricher(TmdbApi tmdbApi, TmdbOptions options) : IMetadataEnricher
{
	public async Task<MediaMatch?> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
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
				MediaType.Movie => MediaMatch.FromMedia(MapMovie(details, tmdbId)),
				MediaType.Show => MediaMatch.FromMedia(MapShow(details, tmdbId)),
				_ => (MediaMatch?)null,
			};
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	private PartialMovie MapMovie(TmdbTypes.Details details, TmdbId sourceId) =>
		new()
		{
			Identity = new MediaIdentity(default, [sourceId]),
			Title = details.Title.NullIfBlank(),
			Tagline = details.Tagline.NullIfBlank(),
			Summary = details.Overview.NullIfBlank(),
			PosterUrl = ComposeImageUrl(details.PosterPath).NullIfBlank(),
			BackdropUrl = ComposeImageUrl(details.BackdropPath).NullIfBlank(),
			ReleaseDate = ParseReleaseDate(details.ReleaseDate ?? details.FirstAirDate),
			Genres = [.. (details.Genres ?? []).Select(g => g.Name).Where(name => !string.IsNullOrWhiteSpace(name))],
			Runtime = details.Runtime is > 0 ? TimeSpan.FromMinutes(details.Runtime.Value) : null,
		};

	private PartialShow MapShow(TmdbTypes.Details details, TmdbId sourceId) =>
		new()
		{
			Identity = new MediaIdentity(default, [sourceId]),
			Title = details.Name.NullIfBlank(),
			Tagline = details.Tagline.NullIfBlank(),
			Summary = details.Overview.NullIfBlank(),
			PosterUrl = ComposeImageUrl(details.PosterPath).NullIfBlank(),
			BackdropUrl = ComposeImageUrl(details.BackdropPath).NullIfBlank(),
			ReleaseDate = ParseReleaseDate(details.FirstAirDate ?? details.ReleaseDate),
			Genres = [.. (details.Genres ?? []).Select(g => g.Name).Where(name => !string.IsNullOrWhiteSpace(name))],
			Seasons = details.NumberOfSeasons,
			Episodes = details.NumberOfEpisodes,
			RuntimePerEpisode = details.EpisodeRunTime is { Count: > 0 } runtimes && runtimes[0] > 0 ? TimeSpan.FromMinutes(runtimes[0]) : null,
		};

	private string ComposeImageUrl(string? path) => string.IsNullOrWhiteSpace(path) ? string.Empty : options.ImageBaseUrl + path;

	private static DateOnly? ParseReleaseDate(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : null;
}
