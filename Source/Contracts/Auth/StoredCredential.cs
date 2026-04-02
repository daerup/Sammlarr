namespace Contracts.Auth;

public record StoredCredential(AuthProvider AuthProvider, Guid ClientId, string AccessToken, DateTimeOffset ExpiresAt);
