namespace Contracts.Identity;

public sealed record IssuedLink(string Path, TimeSpan Lifetime);

public sealed record IssuedToken(string Token, TimeSpan Lifetime);
