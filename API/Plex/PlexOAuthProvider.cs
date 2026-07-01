using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using API.Plex.Types;
using Contracts;
using Contracts.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Shared.Options;

namespace API.Plex;

internal class PlexOAuthProvider(IHttpClientFactory httpClientFactory, PlexOptions options, PlexClient plexClient) : IOAuthProvider
{
	public async Task<string> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default)
	{
		var clientId = Guid.NewGuid();
		using var request = new HttpRequestMessage(HttpMethod.Post, PlexEndpoints.Pins);
		request.Headers.AddClientId(clientId);
		request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["strong"] = "true" });
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

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
			["context%5Bdevice%5D%5Bproduct%5D"] = options.Authentication.ProductName,
			["forwardUrl"] = forwardUrl,
		};
		return QueryHelpers.AddQueryString(PlexEndpoints.AuthApp, queryParams).Replace("?", "#?");
	}

	public async Task<Result<AuthGrant>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default)
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

		using var request = new HttpRequestMessage(HttpMethod.Get, $"{PlexEndpoints.Pins}/{pinId}");
		request.Headers.AddClientId(clientId);
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Auth.Root>(stream, cancellationToken: cancellationToken))!;

		var authToken = result.AuthToken?.ToString();
		if (string.IsNullOrEmpty(authToken))
		{
			return Error.From("Authentication was not completed. Please try again.");
		}

		return new AuthGrant(authToken, result.ExpiresAt, clientId);
	}

	public async Task<AuthSession> GetUserAsync(CancellationToken cancellationToken = default)
	{
		HttpResponseMessage response;
		try
		{
			response = await plexClient.SendAsync(HttpMethod.Get, PlexEndpoints.User, cancellationToken);
		}
		catch
		{
			return new Unavailable();
		}

		if (response.StatusCode == HttpStatusCode.Unauthorized)
		{
			return new SignedOut();
		}
		if (!response.IsSuccessStatusCode)
		{
			return new Unavailable();
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var user = await JsonSerializer.DeserializeAsync<PlexTypes.User.Root>(stream, cancellationToken: cancellationToken);
		if (user is null)
		{
			return new Unavailable();
		}

		var displayName =
			!string.IsNullOrWhiteSpace(user.FriendlyName) ? user.FriendlyName
			: !string.IsNullOrWhiteSpace(user.Title) ? user.Title
			: user.Username;

		var joinedAt = user.JoinedAt is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : default;
		var subscriptionDescription = user.Subscription?.Active == true ? user.SubscriptionDescription ?? string.Empty : string.Empty;
		return new AuthorizedUser(
			DisplayName: displayName,
			AvatarUrl: user.Thumb ?? string.Empty,
			Email: user.Email ?? string.Empty,
			SubscriptionDescription: subscriptionDescription,
			JoinedAt: joinedAt,
			AccountUrl: "https://watch.plex.tv/de/account/profile"
		);
	}
}

file static class PlexHttpRequestHeadersExtensions
{
	extension(HttpRequestHeaders headers)
	{
		public void AddClientId(Guid clientId) => headers.Add("X-Plex-Client-Identifier", clientId.ToString());
	}
}
