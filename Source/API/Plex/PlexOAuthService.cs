using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Auth;
using DB.Data.Stores;
using Microsoft.AspNetCore.WebUtilities;
using Shared.Options;

namespace API.Plex;

public sealed class PlexOAuthService(ICredentialStore credentialStore, IHttpClientFactory httpClientFactory, PlexOptions options, PlexClient plexClient)
	: IOAuthService
{
	private const string PlexAuthAppUrl = "https://app.plex.tv/auth";
	private const string PlexPinEndpoint = "https://plex.tv/api/v2/pins";
	private const string PlexUserEndpoint = "https://plex.tv/api/v2/user";

	public async Task<string> GetOAuthUrl(string forwardUrl, CancellationToken cancellationToken = default)
	{
		var credential = await credentialStore.GetAsync(AuthProvider.Plex, cancellationToken);
		if (credential is null || credential.ExpiresAt < DateTimeOffset.UtcNow)
		{
			return await AuthenticateAsync(forwardUrl, cancellationToken);
		}

		return string.Empty;
	}

	private async Task<string> AuthenticateAsync(string forwardUrl, CancellationToken cancellationToken = default)
	{
		var clientId = Guid.NewGuid();
		using var request = new HttpRequestMessage(HttpMethod.Post, PlexPinEndpoint);
		request.Headers.AddClientId(clientId);
		request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["strong"] = "true" });
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Auth.Root>(stream, cancellationToken: cancellationToken))!;

		var formattedForwardUrl = string.Format(forwardUrl, result.PinId, clientId);
		var queryParams = new Dictionary<string, string?>
		{
			["clientID"] = clientId.ToString(),
			["code"] = result.Code,
			["context%5Bdevice%5D%5Bproduct%5D"] = options.Authentication.ProductName,
			["forwardUrl"] = formattedForwardUrl,
		};
		var authUrl = QueryHelpers.AddQueryString(PlexAuthAppUrl, queryParams).Replace("?", "#?");
		return authUrl;
	}

	public async Task<AuthPinResponse> CallbackAsync(int pinId, Guid clientId, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{PlexPinEndpoint}/{pinId}");
		request.Headers.AddClientId(clientId);
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Auth.Root>(stream, cancellationToken: cancellationToken))!;

		return new AuthPinResponse
		{
			Id = pinId,
			AuthToken = result.AuthToken.ToString(),
			ExpiresAt = result.ExpiresAt,
		};
	}

	public async Task<AuthUserInfo?> GetLoggedInUser()
	{
		var response = await plexClient.SendAsync(HttpMethod.Get, PlexUserEndpoint);
		if (!response.IsSuccessStatusCode)
			return null;

		await using var stream = await response.Content.ReadAsStreamAsync();
		var user = await JsonSerializer.DeserializeAsync<PlexTypes.User.Root>(stream);
		if (user is null)
			return null;

		return new AuthUserInfo(string.IsNullOrWhiteSpace(user.Title) ? user.Username : user.Title, user.Thumb);
	}
}

file static class PlexHttpRequestHeadersExtensions
{
	extension(HttpRequestHeaders headers)
	{
		public void AddClientId(Guid clientId)
		{
			headers.Add("X-Plex-Client-Identifier", clientId.ToString());
		}
	}
}
