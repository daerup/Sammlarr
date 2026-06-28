using System.Globalization;
using System.Text.Json;
using API.Plex.Types;
using Contracts;
using Contracts.Media;
using Contracts.Media.Search;

namespace API.Plex;

internal class PlexApi(PlexClient plexClient) : IMediaSearchProvider
{
	public async Task<MediaMatch> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default)
	{
		if (mediaId is not PlexId plexId)
		{
			throw new ArgumentException($"PlexApi only supports {nameof(PlexId)} lookups.", nameof(mediaId));
		}

		return await ByIdAsync(plexId, cancellationToken);
	}

	public async Task<IReadOnlyList<MediaMatch>> SearchAsync(
		string title,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var searchTypes = string.Join(",", mediaTypes.Select(ToPlexMediaType));
		var endpoint = string.Format(CultureInfo.InvariantCulture, PlexEndpoints.Search, Uri.EscapeDataString(searchTypes), Uri.EscapeDataString(title));
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Search.Root>(stream, cancellationToken: cancellationToken))!;
		if (result.MediaContainer.Size is 1 && result.MediaContainer.SearchResults[0].Size is 0)
		{
			return [];
		}

		return result
			.MediaContainer.SearchResults.SelectMany(c => c.NestedSearchResult)
			.Select(s => s.Metadata.ToMediaResult())
			.DistinctBy(r => r.Externals.OfType<PlexId>().FirstOrDefault())
			.ToList();
	}

	internal async Task<IReadOnlyList<IExternalMediaId>> LookUpExternalsAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		var raw = await ByIdAsync(plexId, cancellationToken);
		return [.. raw.Externals];
	}

	internal async Task<MediaMatch> ByIdAsync(PlexId plexId, CancellationToken cancellationToken = default)
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
		return metadata[0].ToMedia();
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
	public static MediaMatch ToMediaResult(this PlexTypes.Search.Metadata result) =>
		ParseMediaType(result.Type) switch
		{
			MediaType.Movie => MediaMatch.FromMedia(MapMovieResult(result)),
			MediaType.Show => MediaMatch.FromMedia(MapShowResult(result)),
			_ => MapUnmatched(result),
		};

	public static MediaMatch ToMedia(this PlexTypes.ById.Metadata result) =>
		ParseMediaType(result.Type) switch
		{
			MediaType.Movie => MediaMatch.FromMedia(MapMovieResult(result)),
			MediaType.Show => MediaMatch.FromMedia(MapShowResult(result)),
			_ => MapUnmatched(result),
		};

	private static PartialMovie MapMovieResult(PlexTypes.ById.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, [.. BuildExternals(result.PlexId, result.TmdbIds, result.ImdbIds, result.TvdbIds)]),
			Title = result.Title.NullIfBlank(),
			Tagline = result.Tagline.NullIfBlank(),
			Summary = result.Summary.NullIfBlank(),
			PosterUrl = NormalizePlexImageUrl(result.Poster).NullIfBlank(),
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank(),
			Runtime = result.Duration > 0 ? TimeSpan.FromMilliseconds(result.Duration) : null,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static PartialShow MapShowResult(PlexTypes.ById.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, [.. BuildExternals(result.PlexId, result.TmdbIds, result.ImdbIds, result.TvdbIds)]),
			Title = result.Title.NullIfBlank(),
			Tagline = result.Tagline.NullIfBlank(),
			Summary = result.Summary.NullIfBlank(),
			Seasons = result.SeasonCount,
			Episodes = result.EpisodeCount,
			PosterUrl = NormalizePlexImageUrl(result.Poster).NullIfBlank(),
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank(),
			RuntimePerEpisode = result.Duration > 0 ? TimeSpan.FromMilliseconds(result.Duration) : null,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static PartialMovie MapMovieResult(PlexTypes.Search.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, [result.PlexId]),
			Title = result.Title.NullIfBlank(),
			PosterUrl = NormalizePlexImageUrl(result.Thumb).NullIfBlank(),
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank(),
			Runtime = result.Duration is > 0 ? TimeSpan.FromMilliseconds(result.Duration.Value) : null,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static PartialShow MapShowResult(PlexTypes.Search.Metadata result) =>
		new()
		{
			Identity = new MediaIdentity(default, [result.PlexId]),
			Title = result.Title.NullIfBlank(),
			Seasons = result.SeasonCount,
			Episodes = result.EpisodeCount,
			PosterUrl = NormalizePlexImageUrl(result.Thumb).NullIfBlank(),
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank(),
			RuntimePerEpisode = result.Duration is > 0 ? TimeSpan.FromMilliseconds(result.Duration.Value) : null,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static UnmatchedMedia MapUnmatched(PlexTypes.ById.Metadata result) =>
		new()
		{
			Externals = [.. BuildExternals(result.PlexId, result.TmdbIds, result.ImdbIds, result.TvdbIds)],
			Title = result.Title.NullIfBlank() ?? "",
			Summary = result.Summary.NullIfBlank() ?? "",
			PosterUrl = NormalizePlexImageUrl(result.Poster).NullIfBlank() ?? "",
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank() ?? "",
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static UnmatchedMedia MapUnmatched(PlexTypes.Search.Metadata result) =>
		new()
		{
			Externals = [result.PlexId],
			Title = result.Title.NullIfBlank() ?? "",
			PosterUrl = NormalizePlexImageUrl(result.Thumb).NullIfBlank() ?? "",
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank() ?? "",
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static List<IExternalMediaId> BuildExternals(PlexId plex, IEnumerable<TmdbId> tmdb, IEnumerable<ImdbId> imdb, IEnumerable<TvdbId> tvdb)
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

	private static IEnumerable<string> GenreTags(IEnumerable<string>? tags) =>
		(tags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase);

	private static MediaType ParseMediaType(string value) =>
		value.ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"show" => MediaType.Show,
			_ => MediaType.Unknown,
		};

	private static DateOnly? ParseReleaseDate(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : null;

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
