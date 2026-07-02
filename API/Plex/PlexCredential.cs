using Contracts;
using Domain.Auth;
using Domain.Providers;

namespace API.Plex;

internal sealed class PlexCredential : ICredential
{
	private PlexCredential(Guid clientId, string accessToken, DateTimeOffset expiresAt)
	{
		ClientId = clientId;
		AccessToken = accessToken;
		ExpiresAt = expiresAt;
	}

	public WatchlistProvider Provider => WatchlistProvider.Plex;

	public Guid ClientId { get; }

	public string AccessToken { get; }

	public DateTimeOffset ExpiresAt { get; }

	public static async Task<Result<ICredential>> CreateAsync(
		Guid clientId,
		string accessToken,
		DateTimeOffset expiresAt,
		IHttpClientFactory httpClientFactory,
		CancellationToken cancellationToken = default
	)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, PlexEndpoints.User);
		request.Headers.Add("X-Plex-Token", accessToken);
		request.Headers.Add("X-Plex-Client-Identifier", clientId.ToString());
		var response = await httpClientFactory.CreateClient(PlexClient.Unauthenticated).SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return Error.From("The Plex credential is not valid.");
		}

		return new PlexCredential(clientId, accessToken, expiresAt);
	}

	public override string ToString() => $"Credential({Provider}, ****)";
}
