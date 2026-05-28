namespace Contracts.Auth;

public record StoredCredential(WatchlistProvider WatchlistProvider, Guid ClientId, string AccessToken, DateTimeOffset ExpiresAt);
