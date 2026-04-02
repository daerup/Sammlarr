using System.Text.Json;

namespace API.Plex;

public record PlexTokenResponse(string AccessToken, string? RefreshToken, string TokenType, int ExpiresIn)
{
	public static PlexTokenResponse FromJson(JsonElement root)
	{
		if (!root.TryGetProperty("access_token", out var accessTokenElement))
		{
			throw new InvalidOperationException("Plex did not return an access token.");
		}

		var accessToken = accessTokenElement.GetString() ?? throw new InvalidOperationException("Plex access token was empty.");
		var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement) ? refreshTokenElement.GetString() : null;
		var tokenType = root.TryGetProperty("token_type", out var tokenTypeElement) ? tokenTypeElement.GetString() ?? "Bearer" : "Bearer";
		var expiresIn = root.TryGetProperty("expires_in", out var expiresElement) && expiresElement.TryGetInt32(out var seconds) ? seconds : 3600;

		return new PlexTokenResponse(accessToken, refreshToken, tokenType, expiresIn);
	}
}
