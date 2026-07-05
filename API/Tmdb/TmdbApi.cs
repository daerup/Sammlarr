using System.Text.Json;
using API.Tmdb.Types;
using Contracts.Auth;
using Contracts.Media;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace API.Tmdb;

internal class TmdbApi(TmdbClient tmdbClient, IApiKeyConfigRepository apiKeyConfigRepository, ILogger<TmdbApi> logger)
{
	public async Task<TmdbTypes.Details?> GetDetailAsync(TmdbId id, CancellationToken cancellationToken = default)
	{
		var endpoint = string.Format(TmdbEndpoints.Detail, ToTmdbMediaType(id.Type), id.Value);
		var apiKey = await GetApiKeyAsync(cancellationToken);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning(
				"TMDB detail returned {StatusCode} for {MediaId}; treated as no metadata (check API key / rate limits)",
				(int)response.StatusCode,
				id.Value
			);
		}
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
		var endpoint = string.Format(TmdbEndpoints.Find, id.Value);
		var apiKey = await GetApiKeyAsync(cancellationToken);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning("TMDB find returned {StatusCode} for {ImdbId}; treated as no match (check API key / rate limits)", (int)response.StatusCode, id.Value);
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Find>(stream);
		return id.Type switch
		{
			MediaType.Movie => result?.MovieResults.FirstOrDefault(),
			MediaType.Show => result?.TvResults.FirstOrDefault(),
			_ => null,
		};
	}

	private async Task<IApiKey> GetApiKeyAsync(CancellationToken cancellationToken) =>
		await apiKeyConfigRepository
			.GetApiKeyAsync(ApiKeyProvider.Tmdb, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No TMDB API key is configured. Add one in Settings → API Keys."));

	private static string ToTmdbMediaType(MediaType mediaType) =>
		mediaType switch
		{
			MediaType.Movie => TmdbEndpoints.MovieType,
			MediaType.Show => TmdbEndpoints.ShowType,
			_ => throw new ArgumentOutOfRangeException(nameof(mediaType), $"Unsupported media type: {mediaType}"),
		};
}
