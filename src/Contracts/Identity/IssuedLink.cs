namespace Contracts.Identity;

public sealed record IssuedLink(Secret Path, TimeSpan Lifetime);

public sealed record IssuedToken(Secret Token, TimeSpan Lifetime);
