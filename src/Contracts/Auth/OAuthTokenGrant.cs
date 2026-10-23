namespace Contracts.Auth;

public record OAuthTokenGrant(Secret AccessToken, DateTimeOffset ExpiresAt, Guid ClientId);
