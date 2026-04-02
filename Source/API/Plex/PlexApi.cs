using System.Globalization;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;

namespace API.Plex;

internal class PlexApi(PlexClient plexClient) : IMediaSearchService
{
	private const string RatingKeyMetadataEndpoint = "https://metadata.provider.plex.tv/library/metadata/{0}?includeGuids=1&includeExternalMedia=1";
	private const string SerachMetadataEndpoint =
		"https://discover.provider.plex.tv/library/search?searchTypes={0}&searchProviders=discover&includeMetadata=1&query={1}";

	public async Task<PlexTypes.ById.Metadata> ByIdAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(RatingKeyMetadataEndpoint, Uri.EscapeDataString(plexId.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync<PlexTypes.ById.Root>(stream, cancellationToken: cancellationToken);
		return result!.MediaContainer.Metadata[0];
	}

	public async Task<IReadOnlyList<MediaSearchResult>> SearchAsync(
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
			.Select(s => MapSearchResult(s.Metadata))
			.DistinctBy(result => result.SourceId)
			.ToList();
	}

	private static MediaSearchResult MapSearchResult(PlexTypes.Search.Metadata result)
	{
		return new MediaSearchResult(result.PlexId, result.Title)
		{
			MediaType = ParseMediaType(result.Type),
			Year = result.Year > 0 ? result.Year : null,
			PosterUrl = NormalizePlexImageUrl(result.Thumb),
			Description = result.Summary,
			Tagline = result.Tagline,
			Rating = result.Rating,
			AudienceRating = result.AudienceRating,
			Runtime = result.Duration is > 0 ? TimeSpan.FromMilliseconds(result.Duration.Value) : null,
			ReleaseDate = ParseReleaseDate(result.OriginallyAvailableAt),
			Genres = (result.Genre ?? [])
				.Select(genre => genre.Tag)
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Take(4)
				.ToList(),
		};
	}

	private static MediaType ParseMediaType(string value)
	{
		return value.ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"show" => MediaType.Show,
			_ => MediaType.Unknown,
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
