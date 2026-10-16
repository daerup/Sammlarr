namespace Contracts.Auth;

public record OAuthTokenGrant([property: Sensitive] string AccessToken, DateTimeOffset ExpiresAt, Guid ClientId);
