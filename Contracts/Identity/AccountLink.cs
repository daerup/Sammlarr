using Domain.Identity;

namespace Contracts.Identity;

public sealed record AccountLink(AccountLinkKind Kind, string Username, string DisplayName, Optional<UserId> User, string IssuedBy, DateTimeOffset ExpiresAt);

public sealed record IssuedLink(string Path, DateTimeOffset ExpiresAt);
