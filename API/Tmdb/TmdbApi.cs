using System.Text.Json;
using API.Http;
using API.Tmdb.Types;
using Contracts;
using Contracts.Auth;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace API.Tmdb;

internal sealed class TmdbApi(TmdbClient tmdbClient, IApiKeyService apiKeys, IDateTimeProvider clock, ILogger<TmdbApi> logger)
{
	private readonly RecentAnswers<TmdbId, TmdbTypes.Details> _details = new(clock);

	public Task<Result<TmdbTypes.Details>> GetDetailWithExternalIdsAsync(TmdbId id, CancellationToken cancellationToken = default)
	{
		return _details.AskAsync(id, token => DetailAsync(id, token), cancellationToken);
	}

	public async Task<Result<TmdbTypes.FindItem>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return await (await apiKeys.GetAsync(ApiKeyProvider.Tmdb, cancellationToken)).Map(apiKey => FetchFindAsync(id, apiKey, cancellationToken));
	}

	public async Task<Result<IReadOnlyList<TmdbTypes.Trending.Result>>> SearchAsync(string query, CancellationToken cancellationToken = default)
	{
		return await (await apiKeys.GetAsync(ApiKeyProvider.Tmdb, cancellationToken)).Map(apiKey => FetchSearchAsync(query, apiKey, cancellationToken));
	}

	private async Task<Result<TmdbTypes.Details>> DetailAsync(TmdbId id, CancellationToken cancellationToken)
	{
		return await (await apiKeys.GetAsync(ApiKeyProvider.Tmdb, cancellationToken)).Map(apiKey => FetchDetailAsync(id, apiKey, cancellationToken));
	}

	private async Task<Result<IReadOnlyList<TmdbTypes.Trending.Result>>> FetchSearchAsync(string query, IApiKey apiKey, CancellationToken cancellationToken)
	{
		var endpoint = string.Format(TmdbEndpoints.Search, Uri.EscapeDataString(query));
		using var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning("TMDB search returned {StatusCode} for {Query}", (int)response.StatusCode, query);
			return response.StatusCode.Unanswered(RemoteService.Tmdb, $"TMDB search returned {(int)response.StatusCode}.");
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync<TmdbTypes.Trending.Root>(stream, cancellationToken: cancellationToken);
		return Success.Of(result?.Results ?? []);
	}

	private async Task<Result<TmdbTypes.Details>> FetchDetailAsync(TmdbId id, IApiKey apiKey, CancellationToken cancellationToken)
	{
		if (ToTmdbMediaType(id.Type) is not { } mediaType)
		{
			return Error.From($"TMDB does not support media type {id.Type}.");
		}
		var endpoint = string.Format(TmdbEndpoints.DetailWithExternalIds, mediaType, id.Value);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning(
				"TMDB detail returned {StatusCode} for {MediaId}; treated as no details (check API key / rate limits)",
				(int)response.StatusCode,
				id.Value
			);
			return response.StatusCode.Unanswered(RemoteService.Tmdb, $"TMDB detail returned {(int)response.StatusCode} for {id.Value}");
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var reason = $"TMDB has no details for {id.Value}";
		return Result
			.WhenNotNull(await JsonSerializer.DeserializeAsync<TmdbTypes.Details>(stream, cancellationToken: cancellationToken), reason)
			.When(detail => detail.Id != 0, reason);
	}

	private static string? FindSource(IExternalMediaId id) =>
		id switch
		{
			ImdbId => "imdb_id",
			TvdbId => "tvdb_id",
			_ => null,
		};

	private async Task<Result<TmdbTypes.FindItem>> FetchFindAsync(IExternalMediaId id, IApiKey apiKey, CancellationToken cancellationToken)
	{
		if (FindSource(id) is not { } source)
		{
			return Error.From($"TMDB cannot find a title by a {id.Source} id.");
		}
		var endpoint = string.Format(TmdbEndpoints.Find, id.Value, source);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning("TMDB find returned {StatusCode} for {ExternalId}", (int)response.StatusCode, id.Value);
			return response.StatusCode.Unanswered(RemoteService.Tmdb, $"TMDB find returned {(int)response.StatusCode} for {id.Value}");
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync<TmdbTypes.Find>(stream, cancellationToken: cancellationToken);
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
