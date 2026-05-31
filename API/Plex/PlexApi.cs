using System.Globalization;
using System.Text.Json;
using API.Plex.Types;
using Contracts;
using Contracts.Media;
using Contracts.Media.Search;

namespace API.Plex;

internal class PlexApi(PlexClient plexClient) : IMediaSearchProvider
{
	public async Task<IMediaDetail> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default)
	{
		if (mediaId is not PlexId plexId)
		{
			throw new ArgumentException($"PlexApi only supports {nameof(PlexId)} lookups.", nameof(mediaId));
		}

		return await ByIdAsync(plexId, cancellationToken);
	}

	public async Task<IReadOnlyList<IMediaDetail>> SearchAsync(
		string title,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var searchTypes = string.Join(",", mediaTypes.Select(ToPlexMediaType));
		var endpoint = string.Format(CultureInfo.InvariantCulture, PlexEndpoints.Search, Uri.EscapeDataString(searchTypes), Uri.EscapeDataString(title));
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Search.Root>(stream, cancellationToken: cancellationToken))!;
		if (result.MediaContainer.Size is 1 && result.MediaContainer.SearchResults[0].Size is 0)
		{
			return [];
		}

		return result
			.MediaContainer.SearchResults.SelectMany(c => c.NestedSearchResult)
			.Select(s => s.Metadata.ToMediaDetailResult())
			.DistinctBy(r => r.Identity.Externals.OfType<PlexId>().FirstOrDefault())
			.ToList();
	}

	internal async Task<IReadOnlyList<IExternalMediaId>> LookUpExternalsAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		var raw = await ByIdAsync(plexId, cancellationToken);
		return raw.Identity.Externals;
	}

	private async Task<IMediaDetail> ByIdAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(PlexEndpoints.RatingKeyMetadata, Uri.EscapeDataString(plexId.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync<PlexTypes.ById.Root>(stream, cancellationToken: cancellationToken);
		if (result?.MediaContainer?.Metadata is not { Count: > 0 } metadata)
		{
			throw new InvalidOperationException($"Plex returned no metadata for ratingKey {plexId.Value}.");
		}
		return metadata[0].ToMediaDetailResult();
	}

	internal async Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveExternalsByPlexAsync(
		PlexId plexId,
		Func<IExternalMediaId, bool> requires,
		CancellationToken cancellationToken = default
	)
	{
		var externals = await LookUpExternalsAsync(plexId, cancellationToken);
		return externals.Any(requires) ? Some.Of(externals) : new None();
	}

	private static string ToPlexMediaType(MediaType mediaType) =>
		mediaType switch
		{
			MediaType.Movie => "movies",
			MediaType.Show => "tv",
			_ => throw new ArgumentOutOfRangeException(nameof(mediaType), $"Unsupported media type: {mediaType}"),
		};
}

file static class PlexMetadataMappingExtensions
{
	public static IMediaDetail ToMediaDetailResult(this PlexTypes.Search.Metadata result)
	{
		var mediatype = ParseMediaType(result.Type);
		return mediatype switch
		{
			MediaType.Movie => MapMovieResult(result),
			MediaType.Show => MapShowResult(result),
		};
	}

	public static IMediaDetail ToMediaDetailResult(this PlexTypes.ById.Metadata result)
	{
		var mediatype = ParseMediaType(result.Type);
		return mediatype switch
		{
			MediaType.Movie => MapMovieResult(result),
			MediaType.Show => MapShowResult(result),
		};
	}

	private static MovieDetail MapMovieResult(PlexTypes.ById.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, BuildExternals(result.PlexId, result.TmdbIds, result.ImdbIds, result.TvdbIds)),
			Title = result.Title,
			Tagline = result.Tagline ?? string.Empty,
			Summary = result.Summary ?? string.Empty,
			PosterUrl = NormalizePlexImageUrl(result.Poster),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			Runtime = TimeSpan.FromMilliseconds(result.Duration),
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = (result.Genre ?? [])
				.Select(genre => genre.Tag)
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList(),
		};

	private static ShowDetail MapShowResult(PlexTypes.ById.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, BuildExternals(result.PlexId, result.TmdbIds, result.ImdbIds, result.TvdbIds)),
			Title = result.Title ?? string.Empty,
			Tagline = result.Tagline ?? string.Empty,
			Summary = result.Summary ?? string.Empty,
			Seasons = result.SeasonCount ?? 0,
			Episodes = result.EpisodeCount ?? 0,
			PosterUrl = NormalizePlexImageUrl(result.Poster),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			RuntimePerEpisode = TimeSpan.FromMilliseconds(result.Duration),
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = (result.Genre ?? [])
				.Select(genre => genre.Tag)
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList(),
		};

	private static MovieDetail MapMovieResult(PlexTypes.Search.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, [result.PlexId]),
			Title = result.Title,
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = NormalizePlexImageUrl(result.Thumb),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			Runtime = result.Duration is > 0 ? TimeSpan.FromMilliseconds(result.Duration.Value) : TimeSpan.Zero,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = (result.Genre ?? [])
				.Select(genre => genre.Tag)
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList(),
		};

	private static ShowDetail MapShowResult(PlexTypes.Search.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, [result.PlexId]),
			Title = result.Title,
			Tagline = string.Empty,
			Summary = string.Empty,
			Seasons = result.SeasonCount ?? 0,
			Episodes = result.EpisodeCount ?? 0,
			PosterUrl = NormalizePlexImageUrl(result.Thumb),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			RuntimePerEpisode = result.Duration is > 0 ? TimeSpan.FromMilliseconds(result.Duration.Value) : TimeSpan.Zero,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = (result.Genre ?? [])
				.Select(genre => genre.Tag)
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList(),
		};

	private static IReadOnlyList<IExternalMediaId> BuildExternals(PlexId plex, IEnumerable<TmdbId> tmdb, IEnumerable<ImdbId> imdb, IEnumerable<TvdbId> tvdb)
	{
		var set = new List<IExternalMediaId> { plex };
		foreach (var id in tmdb)
		{
			set.Add(id);
		}
		foreach (var id in imdb)
		{
			set.Add(id);
		}
		foreach (var id in tvdb)
		{
			set.Add(id);
		}
		return set;
	}

	private static MediaType ParseMediaType(string value) =>
		value.ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"show" => MediaType.Show,
		};

	private static DateOnly ParseReleaseDate(string? value) =>
		DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : DateOnly.MinValue;

	private static string NormalizePlexImageUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return string.Empty;
		}

		if (Uri.TryCreate(url, UriKind.Absolute, out _))
		{
			return url;
		}

		return $"https://metadata.provider.plex.tv{url}";
	}
}
