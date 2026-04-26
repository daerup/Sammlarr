using System.Text.Json;
using API.TMDB.Types;
using Contracts.Media;

namespace API.TMDB;

internal class TmdbApi(TmdbClient tmdbClient)
{
	public async Task<TmdbTypes.Details?> GetDetailAsync(TmdbId id, CancellationToken cancellationToken = default)
	{
		var endpoint = $"{ToTmdbMediaType(id.Type)}/{id.Value}";
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Details>(stream);
		if (result is null || result.Id == 0)
		{
			return null;
		}
		return result;
	}

	public async Task<TmdbTypes.FindItem?> FindByImdbIdAsync(ImdbId id, CancellationToken cancellationToken = default)
	{
		var endpoint = $"find/{id.Value}?external_source=imdb_id";
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Find>(stream);
		return id.Type switch
		{
			MediaType.Movie => result?.MovieResults.FirstOrDefault(),
			MediaType.Show => result?.TvResults.FirstOrDefault(),
			_ => null,
		};
	}

	private static string ToTmdbMediaType(MediaType mediaType) =>
		mediaType switch
		{
			MediaType.Movie => "movie",
			MediaType.Show => "tv",
			_ => throw new ArgumentOutOfRangeException(nameof(mediaType), $"Unsupported media type: {mediaType}"),
		};
}
