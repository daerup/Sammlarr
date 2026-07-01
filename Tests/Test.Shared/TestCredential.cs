namespace Test.Shared;

public sealed record TestCredential(WatchlistProvider Provider, Guid ClientId, string AccessToken, DateTimeOffset ExpiresAt) : ICredential;
