using System.Globalization;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Search;

namespace API.Plex;

internal class PlexApi(PlexClient plexClient) : IMediaSearchService
{
	private const string RatingKeyMetadataEndpoint = "https://metadata.provider.plex.tv/library/metadata/{0}?includeGuids=1&includeExternalMedia=1";

	private const string SerachMetadataEndpoint =
		"https://discover.provider.plex.tv/library/search?searchTypes={0}&searchProviders=discover&includeMetadata=1&query={1}";

	public async Task<IMediaDetailResult> ByIdAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(RatingKeyMetadataEndpoint, Uri.EscapeDataString(plexId.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync<PlexTypes.ById.Root>(stream, cancellationToken: cancellationToken);
		return result!.MediaContainer.Metadata[0].ToMediaDetailResult();
	}

	internal async Task<ResolvedId> ResolveById<TMediaId>(PlexId plexId)
		where TMediaId : IMediaId
	{
		var plexResult = await ByIdAsync(plexId);
		var mediaIds = plexResult.AdditionalIds.OfType<TMediaId>().Cast<IMediaId>().ToList();
		var otherIds = plexResult.AdditionalIds.Except(mediaIds).Append(plexResult.SourceId);
		if (mediaIds.Any())
		{
			var id = mediaIds.First();
			var remainingIds = mediaIds.Skip(1);
			return new ResolvedId(id, remainingIds.Concat(otherIds));
		}

		return ResolvedId.Unresolved(otherIds);
	}

	public async Task<IReadOnlyList<IMediaDetailResult>> SearchAsync(
		string title,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var searchTypes = string.Join(",", mediaTypes.Select(ToPlexMediaType));
		var endpoint = string.Format(CultureInfo.InvariantCulture, SerachMetadataEndpoint, Uri.EscapeDataString(searchTypes), Uri.EscapeDataString(title));
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
			.DistinctBy(result => result.SourceId)
			.ToList();
	}

	private static string ToPlexMediaType(MediaType mediaType)
	{
		return mediaType switch
		{
			MediaType.Movie => "movies",
			MediaType.Show => "tv",
			_ => throw new ArgumentOutOfRangeException(nameof(mediaType), $"Unsupported media type: {mediaType}"),
		};
	}
}

file static class PlexMetadataMappingExtensions
{
	public static IMediaDetailResult ToMediaDetailResult(this PlexTypes.Search.Metadata result)
	{
		var mediatype = ParseMediaType(result.Type);
		return mediatype switch
		{
			MediaType.Movie => MapMovieResult(result),
			MediaType.Show => MapShowResult(result),
		};
	}

	public static IMediaDetailResult ToMediaDetailResult(this PlexTypes.ById.Metadata result)
	{
		var mediatype = ParseMediaType(result.Type);
		return mediatype switch
		{
			MediaType.Movie => MapMovieResult(result),
			MediaType.Show => MapShowResult(result),
		};
	}

	private static MovieDetailResult MapMovieResult(PlexTypes.ById.Metadata result)
	{
		return new MovieDetailResult
		{
			Title = result.Title,
			SourceId = result.PlexId,
			AdditionalIds = [.. result.TmdbIds, .. result.ImdbIds, .. result.TvdbIds],
			Year = result.Year > 0 ? result.Year : null,
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
	}

	private static ShowDetailResult MapShowResult(PlexTypes.ById.Metadata result)
	{
		return new ShowDetailResult
		{
			Title = result.Title,
			SourceId = result.PlexId,
			AdditionalIds = [.. result.TmdbIds, .. result.ImdbIds, .. result.TvdbIds],
			Seasons = result.SeasonCount ?? 0,
			Episodes = result.EpisodeCount ?? 0,
			Year = result.Year > 0 ? result.Year : null,
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
	}

	private static MovieDetailResult MapMovieResult(PlexTypes.Search.Metadata result)
	{
		return new MovieDetailResult
		{
			Title = result.Title,
			SourceId = result.PlexId,
			AdditionalIds = [],
			Year = result.Year > 0 ? result.Year : null,
			PosterUrl = NormalizePlexImageUrl(result.Thumb),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			Runtime = result.Duration is > 0 ? TimeSpan.FromMilliseconds(result.Duration.Value) : null,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = (result.Genre ?? [])
				.Select(genre => genre.Tag)
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList(),
		};
	}

	private static ShowDetailResult MapShowResult(PlexTypes.Search.Metadata result)
	{
		return new ShowDetailResult
		{
			Title = result.Title,
			SourceId = result.PlexId,
			AdditionalIds = [],
			Seasons = result.SeasonCount ?? 0,
			Episodes = result.EpisodeCount ?? 0,
			Year = result.Year > 0 ? result.Year : null,
			PosterUrl = NormalizePlexImageUrl(result.Thumb),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			Runtime = result.Duration is > 0 ? TimeSpan.FromMilliseconds(result.Duration.Value) : null,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = (result.Genre ?? [])
				.Select(genre => genre.Tag)
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList(),
		};
	}

	private static MediaType ParseMediaType(string value)
	{
		return value.ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"show" => MediaType.Show,
		};
	}

	private static DateOnly? ParseReleaseDate(string? value)
	{
		return DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : null;
	}

	private static string? NormalizePlexImageUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return null;
		}

		if (Uri.TryCreate(url, UriKind.Absolute, out _))
		{
			return url;
		}

		return $"https://metadata.provider.plex.tv{url}";
	}
}
