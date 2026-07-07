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
	public async Task<Result<TmdbTypes.Details>> GetDetailAsync(TmdbId id, CancellationToken cancellationToken = default) =>
		await apiKeyConfigRepository.GetApiKeyAsync(ApiKeyProvider.Tmdb, cancellationToken).Map(apiKey => FetchDetailAsync(id, apiKey, cancellationToken));

	public async Task<Result<TmdbTypes.FindItem>> FindByImdbIdAsync(ImdbId id, CancellationToken cancellationToken = default) =>
		await apiKeyConfigRepository.GetApiKeyAsync(ApiKeyProvider.Tmdb, cancellationToken).Map(apiKey => FetchFindAsync(id, apiKey, cancellationToken));

	private async Task<Result<TmdbTypes.Details>> FetchDetailAsync(TmdbId id, IApiKey apiKey, CancellationToken cancellationToken)
	{
		if (ToTmdbMediaType(id.Type) is not { } mediaType)
		{
			return Error.From($"TMDB does not support media type {id.Type}.");
		}
		var endpoint = string.Format(TmdbEndpoints.Detail, mediaType, id.Value);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning(
				"TMDB detail returned {StatusCode} for {MediaId}; treated as no metadata (check API key / rate limits)",
				(int)response.StatusCode,
				id.Value
			);
			return Error.From($"TMDB detail returned {(int)response.StatusCode} for {id.Value}");
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var reason = $"TMDB has no metadata for {id.Value}";
		return Result.WhenNotNull(JsonSerializer.Deserialize<TmdbTypes.Details>(stream), reason).When(detail => detail.Id != 0, reason);
	}

	private async Task<Result<TmdbTypes.FindItem>> FetchFindAsync(ImdbId id, IApiKey apiKey, CancellationToken cancellationToken)
	{
		var endpoint = string.Format(TmdbEndpoints.Find, id.Value);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning("TMDB find returned {StatusCode} for {ImdbId}; treated as no match (check API key / rate limits)", (int)response.StatusCode, id.Value);
			return Error.From($"TMDB find returned {(int)response.StatusCode} for {id.Value}");
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Find>(stream);
		var item = id.Type switch
		{
			MediaType.Movie => result?.MovieResults.FirstOrDefault(),
			MediaType.Show => result?.TvResults.FirstOrDefault(),
			MediaType.Unknown => null,
		};
		return Result.WhenNotNull(item, $"TMDB has no {id.Type} match for {id.Value}");
	}

	private static string? ToTmdbMediaType(MediaType mediaType) =>
		mediaType switch
		{
			MediaType.Movie => TmdbEndpoints.MovieType,
			MediaType.Show => TmdbEndpoints.ShowType,
			MediaType.Unknown => null,
		};
}
