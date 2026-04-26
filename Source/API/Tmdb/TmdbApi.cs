using System.Text.Json;
using API.TMDB.Types;
using Contracts.Media;

namespace API.TMDB;

internal class TmdbApi(TmdbClient tmdbClient)
{
	public async Task<TmdbTypes.Details> GetDetailAsync(TmdbId id, CancellationToken cancellationToken = default)
	{
		var endpoint = $"{ToTmdbMediaType(id.Type)}/{id.Value}";
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Details>(stream);
		return result!;
	}

	private static string ToTmdbMediaType(MediaType mediaType) =>
		mediaType switch
		{
			MediaType.Movie => "movie",
			MediaType.Show => "tv",
			_ => throw new ArgumentOutOfRangeException(nameof(mediaType), $"Unsupported media type: {mediaType}"),
		};
}
