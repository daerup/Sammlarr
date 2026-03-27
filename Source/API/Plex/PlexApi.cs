using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;

namespace API.Plex;

public sealed class PlexApi(PlexClient plexClient)
{
	private const string RatingKeyMetadataEndpoint = "https://metadata.provider.plex.tv/library/metadata/{0}?includeGuids=1&includeExternalMedia=1";
	private const string SerachMetadataEndpoint =
		"https://discover.provider.plex.tv/library/search?searchTypes={0}&searchProviders=discover&includeMetadata=1&query={1}";

	public async Task<PlexTypes.ById.Metadata> ByIdAsync(PlexId plexId)
	{
		var endpoint = string.Format(RatingKeyMetadataEndpoint, Uri.EscapeDataString(plexId.Value));
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint);
		await using var stream = await response.Content.ReadAsStreamAsync();
		var result = await JsonSerializer.DeserializeAsync<PlexTypes.ById.Root>(stream);
		return result!.MediaContainer.Metadata[0];
	}

	public async Task<List<PlexTypes.Search.Metadata>> SearchAsync(string title, params IEnumerable<MediaType> mediaType)
	{
		var searchTypes = string.Join(",", mediaType.Select(ToPlexMediaType));
		var endpoint = string.Format(
			System.Globalization.CultureInfo.InvariantCulture,
			SerachMetadataEndpoint,
			Uri.EscapeDataString(searchTypes),
			Uri.EscapeDataString(title)
		);
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint);
		await using var stream = await response.Content.ReadAsStreamAsync();
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Search.Root>(stream))!;
		if (result.MediaContainer.Size is 1 && result.MediaContainer.SearchResults[0].Size is 0)
		{
			return [];
		}
		return result.MediaContainer.SearchResults.SelectMany(c => c.NestedSearchResult).Select(s => s.Metadata).ToList();
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
