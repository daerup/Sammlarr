namespace Contracts.Auth;

public sealed record StoredCredential(AuthProvider AuthProvider, Guid ClientId, string AccessToken, DateTimeOffset ExpiresAt);
