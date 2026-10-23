namespace Contracts.Auth;

public sealed record StoredCredential(Secret AccessToken, DateTimeOffset ExpiresAt, Guid ClientId);
