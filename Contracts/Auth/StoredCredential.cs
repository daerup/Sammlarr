namespace Contracts.Auth;

public sealed record StoredCredential(string AccessToken, DateTimeOffset ExpiresAt, Guid ClientId);
