using System.Net.Http.Headers;
using System.Text.Json;
using API.Http;
using API.Plex.Types;
using Contracts.Auth;
using Domain.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Shared.Options;

namespace API.Plex;

internal sealed class PlexOAuthProvider(IHttpClientFactory httpClientFactory, PlexOptions options, PlexUrls urls) : IOAuthProvider, ICredentialValidator
{
	public async Task<Result<string>> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default)
	{
		var clientId = Guid.NewGuid();
		using var request = new HttpRequestMessage(HttpMethod.Post, urls.Pins);
		request.Headers.AddClientId(clientId);
		request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["strong"] = "true" });
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return Error.From($"Plex could not start the sign-in (it answered {(int)response.StatusCode}). Please try again in a moment.");
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Auth.Root>(stream, cancellationToken: cancellationToken))!;

		var forwardUrl = QueryHelpers.AddQueryString(
			callbackUrl,
			new Dictionary<string, string?> { ["pinId"] = result.PinId.ToString(), ["clientId"] = clientId.ToString() }
		);
		var queryParams = new Dictionary<string, string?>
		{
			["clientID"] = clientId.ToString(),
			["code"] = result.Code,
			["context[device][product]"] = options.Authentication.ProductName,
			["forwardUrl"] = forwardUrl,
		};
		return Success.Of(QueryHelpers.AddQueryString(urls.AuthApp, queryParams).Replace("?", "#?"));
	}

	public async Task<Result<ExternalLogin>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default)
	{
		var query = QueryHelpers.ParseQuery(callbackQuery);
		if (
			!query.TryGetValue("pinId", out var pinIdValue)
			|| !int.TryParse(pinIdValue, out var pinId)
			|| !query.TryGetValue("clientId", out var clientIdValue)
			|| !Guid.TryParse(clientIdValue, out var clientId)
		)
		{
			return Error.From("The login callback is missing its pin or client id.");
		}

		using var request = new HttpRequestMessage(HttpMethod.Get, urls.Pin(pinId));
		request.Headers.AddClientId(clientId);
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var pin = (await JsonSerializer.DeserializeAsync<PlexTypes.Auth.Root>(stream, cancellationToken: cancellationToken))!;

		var authToken = pin.AuthToken?.ToString();
		if (string.IsNullOrEmpty(authToken))
		{
			return Error.From("Authentication was not completed. Please try again.");
		}

		return await FetchAccountAsync(authToken, clientId, pin.ExpiresAt, cancellationToken);
	}

	public async Task<AccessCheck> CheckAsync(Guid clientId, string accessToken, CancellationToken cancellationToken = default)
	{
		using var request = UserRequest(accessToken, clientId);
		return await httpClientFactory.CreateClient(PlexClient.Unauthenticated).CheckAccessAsync(request, cancellationToken);
	}

	private async Task<Result<ExternalLogin>> FetchAccountAsync(string accessToken, Guid clientId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
	{
		using var request = UserRequest(accessToken, clientId);
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return Error.From("Your Plex profile could not be loaded. Please try again.");
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var user = await JsonSerializer.DeserializeAsync<PlexTypes.User.Root>(stream, cancellationToken: cancellationToken);
		if (user is null)
		{
			return Error.From("Your Plex profile could not be loaded. Please try again.");
		}

		var displayName =
			!string.IsNullOrWhiteSpace(user.FriendlyName) ? user.FriendlyName
			: !string.IsNullOrWhiteSpace(user.Title) ? user.Title
			: user.Username;

		return Success.Of(
			new ExternalLogin(user.Uuid ?? user.Username, displayName, user.Thumb ?? string.Empty, new OAuthTokenGrant(accessToken, expiresAt, clientId))
		);
	}

	private HttpRequestMessage UserRequest(string accessToken, Guid clientId)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, urls.User);
		request.Headers.Add("X-Plex-Token", accessToken);
		request.Headers.AddClientId(clientId);
		return request;
	}
}

file static class PlexHttpRequestHeadersExtensions
{
	extension(HttpRequestHeaders headers)
	{
		public void AddClientId(Guid clientId) => headers.Add("X-Plex-Client-Identifier", clientId.ToString());
	}
}
