using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using API.Plex.Types;
using Contracts;
using Contracts.Media;
using Contracts.Media.Search;
using Domain;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace API.Plex;

internal class PlexApi(PlexClient plexClient, PlexUrls urls, ILogger<PlexApi> logger) : IMediaSearchProvider
{
	private const int SearchLimit = 30;

	private readonly ConcurrentDictionary<string, Lazy<Task<Result<MediaLookup>>>> _byRatingKey = new();

	private void EnsureUsable(HttpResponseMessage response, string endpoint)
	{
		if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)response.StatusCode >= 500)
		{
			logger.LogWarning("Plex lookup was rate limited or unavailable ({StatusCode}) on {Endpoint}", (int)response.StatusCode, endpoint);
			throw new ProviderUnavailableException(MediaProvider.Plex);
		}

		response.EnsureSuccessStatusCode();
	}

	public Task<Result<MediaLookup>> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default) =>
		mediaId.Require<PlexId>($"PlexApi only supports {nameof(PlexId)} lookups.").Map(plexId => ByIdAsync(plexId, cancellationToken));

	public async Task<IReadOnlyList<MediaLookup>> SearchAsync(
		string title,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var searchTypes = string.Join(",", mediaTypes.Select(ToPlexMediaType));
		var endpoint = urls.Search(searchTypes, title, SearchLimit);
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		EnsureUsable(response, endpoint);
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

	internal Task<Result<IReadOnlyList<IExternalMediaId>>> LookUpExternalsAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		return ByIdAsync(plexId, cancellationToken).Map<MediaLookup, IReadOnlyList<IExternalMediaId>>(raw => [.. raw.Externals]);
	}

	internal Task<Result<MediaLookup>> ByIdAsync(PlexId plexId, CancellationToken cancellationToken = default) =>
		_byRatingKey.GetOrAdd(plexId.Value, _ => new Lazy<Task<Result<MediaLookup>>>(() => FetchByIdAsync(plexId, cancellationToken))).Value;

	private async Task<Result<MediaLookup>> FetchByIdAsync(PlexId plexId, CancellationToken cancellationToken)
	{
		var endpoint = urls.RatingKeyMetadata(plexId.Value);
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		EnsureUsable(response, endpoint);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync<PlexTypes.ById.Root>(stream, cancellationToken: cancellationToken);
		var reason = $"Plex returned no metadata for ratingKey {plexId.Value}.";
		return Result.WhenNotNull(result?.MediaContainer?.Metadata, reason).When(metadata => metadata.Count > 0, reason).Map(metadata => metadata[0].ToMedia());
	}

	internal Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveExternalsByPlexAsync(
		PlexId plexId,
		Func<IExternalMediaId, bool> requires,
		CancellationToken cancellationToken = default
	) => LookUpExternalsAsync(plexId, cancellationToken).ToOptional().When(list => list.Any(requires));

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
	public static MediaLookup ToMediaResult(this PlexTypes.Search.Metadata result) =>
		ParseMediaType(result.Type) switch
		{
			MediaType.Movie => IdentifiedMedia.From(MapMovieResult(result)),
			MediaType.Show => IdentifiedMedia.From(MapShowResult(result)),
			_ => MapUnmatched(result),
		};

	public static MediaLookup ToMedia(this PlexTypes.ById.Metadata result) =>
		ParseMediaType(result.Type) switch
		{
			MediaType.Movie => IdentifiedMedia.From(MapMovieResult(result)),
			MediaType.Show => IdentifiedMedia.From(MapShowResult(result)),
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
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt) ?? ReleaseYear(result.Year),
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
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt) ?? ReleaseYear(result.Year),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static UnmatchedMedia MapUnmatched(PlexTypes.ById.Metadata result) =>
		new()
		{
			Externals = [.. BuildExternals(result.PlexId, result.TmdbIds, result.ImdbIds, result.TvdbIds)],
			Title = result.Title.NullIfBlank(),
			Summary = result.Summary.NullIfBlank(),
			PosterUrl = NormalizePlexImageUrl(result.Poster).NullIfBlank(),
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank(),
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = [.. GenreTags(result.Genre?.Select(genre => genre.Tag))],
		};

	private static UnmatchedMedia MapUnmatched(PlexTypes.Search.Metadata result) =>
		new()
		{
			Externals = [result.PlexId],
			Title = result.Title.NullIfBlank(),
			PosterUrl = NormalizePlexImageUrl(result.Thumb).NullIfBlank(),
			BackdropUrl = NormalizePlexImageUrl(result.Art).NullIfBlank(),
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt) ?? ReleaseYear(result.Year),
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

	private static DateOnly? ReleaseYear(int year) => year > 0 ? new DateOnly(year, 1, 1) : null;

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
