namespace Domain.Providers;

public union Answer<T>(Found<T>, NoMatch, Unavailable, Faulted) where T : notnull;

public sealed record Found<T>(T Value) where T : notnull;

public sealed record NoMatch(RemoteService Remote);

public sealed record Unavailable(RemoteService Remote, Optional<DateTimeOffset> Until, bool Sent);

public sealed record Faulted(RemoteService Remote, Error Error);
