namespace Contracts.Auth;

public readonly record struct SignedOut;

public readonly record struct Unavailable;

public union AuthSession(AuthorizedUser, SignedOut, Unavailable);
