namespace Contracts.Auth;

public record AuthGrant(string AccessToken, DateTimeOffset ExpiresAt, Guid ClientId);
